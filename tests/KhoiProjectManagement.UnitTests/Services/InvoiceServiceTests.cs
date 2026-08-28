using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    // Upload/template/download paths do real File/Directory I/O (InvoiceService calls File.Copy/
    // File.Exists/FileStream directly - no filesystem abstraction to mock behind), so this class writes
    // to a real temp directory rather than mocking, the same reasoning LogFileServiceTests documents for
    // LogFileService.
    public class InvoiceServiceTests : IDisposable
    {
        private readonly IRepository<Invoice> _invoiceRepo = Substitute.For<IRepository<Invoice>>();
        private readonly IRepository<InvoiceLineItem> _lineItemRepo = Substitute.For<IRepository<InvoiceLineItem>>();
        private readonly IRepository<InvoiceTemplate> _templateRepo = Substitute.For<IRepository<InvoiceTemplate>>();
        private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
        private readonly IActivityLogService _activityLogService = Substitute.For<IActivityLogService>();

        private readonly string _tempDir;
        private readonly string _invoicePath;
        private readonly string _templatePath;

        public InvoiceServiceTests()
        {
            _tempDir = Directory.CreateTempSubdirectory("khoi-invoicetest-").FullName;
            _invoicePath = Path.Combine(_tempDir, "invoice-files");
            _templatePath = Path.Combine(_tempDir, "invoice-templates");
            Directory.CreateDirectory(_invoicePath);
            Directory.CreateDirectory(_templatePath);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        private IConfiguration Config() => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FileUpload:InvoicePath"] = _invoicePath,
                ["FileUpload:InvoiceTemplatePath"] = _templatePath
            })
            .Build();

        private InvoiceService CreateSut() => new(
            _invoiceRepo, _lineItemRepo, _templateRepo, _unitOfWork, Config(), _activityLogService);

        private static IFormFile FakeFile(string fileName, string contentType = "application/pdf", long length = 10)
        {
            var file = Substitute.For<IFormFile>();
            file.FileName.Returns(fileName);
            file.ContentType.Returns(contentType);
            file.Length.Returns(length);
            return file;
        }

        // ----- GetInvoicesAsync / GetInvoiceByIdAsync -----

        [Fact]
        public async Task GetInvoicesAsync_ReturnsEachInvoiceWithATotalComputedFromItsLineItems()
        {
            _invoiceRepo.Query().Returns(new List<Invoice>
            {
                new()
                {
                    Id = 1, InvoiceNumber = "INV-1", ClientName = "Acme", Creator = new User { Name = "Alice" },
                    LineItems = new List<InvoiceLineItem> { new() { Quantity = 2, UnitPrice = 50m } }
                },
                new()
                {
                    Id = 2, InvoiceNumber = "INV-2", ClientName = "Globex", Creator = new User { Name = "Bob" },
                    LineItems = new List<InvoiceLineItem>()
                },
            }.BuildMock());

            var result = await CreateSut().GetInvoicesAsync();

            Assert.Equal(2, result.Count);
            Assert.Equal(100m, result.Single(i => i.Id == 1).Total);
            Assert.Equal(0m, result.Single(i => i.Id == 2).Total);
        }

        [Fact]
        public async Task GetInvoiceByIdAsync_WhenInvoiceDoesNotExist_ReturnsNull()
        {
            _invoiceRepo.Query().Returns(new List<Invoice>().BuildMock());

            var result = await CreateSut().GetInvoiceByIdAsync(999);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetInvoiceByIdAsync_WhenInvoiceExists_ReturnsMappedDto()
        {
            var invoice = new Invoice
            {
                Id = 1, InvoiceNumber = "INV-1", ClientName = "Acme", Status = "Sent",
                Creator = new User { Name = "Alice" },
                LineItems = new List<InvoiceLineItem> { new() { Description = "Work", Quantity = 3, UnitPrice = 10m } }
            };
            _invoiceRepo.Query().Returns(new List<Invoice> { invoice }.BuildMock());

            var result = await CreateSut().GetInvoiceByIdAsync(1);

            Assert.NotNull(result);
            Assert.Equal("INV-1", result!.InvoiceNumber);
            Assert.Equal("Alice", result.CreatorName);
            Assert.Equal(30m, result.Total);
        }

        // ----- CreateInvoiceAsync -----

        [Fact]
        public async Task CreateInvoiceAsync_AddsInvoiceWithItsLineItemsAndReturnsTheSavedDto()
        {
            Invoice? added = null;
            _invoiceRepo.When(r => r.Add(Arg.Any<Invoice>())).Do(ci =>
            {
                added = ci.Arg<Invoice>();
                added.Id = 42;
                added.Creator = new User { Id = 7, Name = "Alice" };
            });
            _invoiceRepo.Query().Returns(_ => new List<Invoice> { added! }.BuildMock());

            var dto = new CreateInvoiceDto
            {
                InvoiceNumber = "INV-100",
                ClientName = "Acme Co",
                IssueDate = new DateTime(2026, 1, 1),
                DueDate = new DateTime(2026, 1, 31),
                LineItems = new List<CreateInvoiceLineItemDto>
                {
                    new() { Description = "Consulting", Quantity = 2, UnitPrice = 100m }
                }
            };

            var result = await CreateSut().CreateInvoiceAsync(dto, createdBy: 7);

            Assert.Equal("INV-100", result.InvoiceNumber);
            Assert.Equal("Alice", result.CreatorName);
            Assert.Equal(200m, result.Total);
            _invoiceRepo.Received(1).Add(Arg.Is<Invoice>(i =>
                i.InvoiceNumber == "INV-100" && i.CreatedBy == 7 && i.LineItems.Count == 1));
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        // ----- UpdateInvoiceAsync -----

        [Fact]
        public async Task UpdateInvoiceAsync_WhenInvoiceDoesNotExist_ReturnsFalse()
        {
            _invoiceRepo.Query().Returns(new List<Invoice>().BuildMock());

            var result = await CreateSut().UpdateInvoiceAsync(999, new UpdateInvoiceDto());

            Assert.False(result);
        }

        [Fact]
        public async Task UpdateInvoiceAsync_WhenInvoiceExists_ReplacesLineItemsAndUpdatesFields()
        {
            var oldLineItem = new InvoiceLineItem { Id = 1, Description = "Old", Quantity = 1, UnitPrice = 50m };
            var invoice = new Invoice
            {
                Id = 1,
                InvoiceNumber = "OLD-1",
                ClientName = "Old Client",
                IssueDate = new DateTime(2026, 1, 1),
                DueDate = new DateTime(2026, 1, 15),
                LineItems = new List<InvoiceLineItem> { oldLineItem }
            };
            _invoiceRepo.Query().Returns(new List<Invoice> { invoice }.BuildMock());

            var dto = new UpdateInvoiceDto
            {
                InvoiceNumber = "NEW-1",
                ClientName = "New Client",
                IssueDate = new DateTime(2026, 2, 1),
                DueDate = new DateTime(2026, 2, 15),
                Notes = "Updated",
                LineItems = new List<CreateInvoiceLineItemDto> { new() { Description = "New", Quantity = 3, UnitPrice = 20m } }
            };

            var result = await CreateSut().UpdateInvoiceAsync(1, dto);

            Assert.True(result);
            Assert.Equal("NEW-1", invoice.InvoiceNumber);
            Assert.Equal("New Client", invoice.ClientName);
            Assert.Equal("Updated", invoice.Notes);
            Assert.Single(invoice.LineItems);
            Assert.Equal("New", invoice.LineItems.First().Description);
            _lineItemRepo.Received(1).RemoveRange(Arg.Is<IEnumerable<InvoiceLineItem>>(items => items.Single() == oldLineItem));
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        // ----- DeleteInvoiceAsync -----

        [Fact]
        public async Task DeleteInvoiceAsync_WhenInvoiceDoesNotExist_ReturnsFalse()
        {
            _invoiceRepo.FindAsync(999).Returns((Invoice?)null);

            var result = await CreateSut().DeleteInvoiceAsync(999);

            Assert.False(result);
        }

        [Fact]
        public async Task DeleteInvoiceAsync_WhenInvoiceExists_RemovesItAndSaves()
        {
            var invoice = new Invoice { Id = 1 };
            _invoiceRepo.FindAsync(1).Returns(invoice);

            var result = await CreateSut().DeleteInvoiceAsync(1);

            Assert.True(result);
            _invoiceRepo.Received(1).Remove(invoice);
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        // ----- UpdateStatusAsync -----

        [Fact]
        public async Task UpdateStatusAsync_WhenStatusIsNotOneOfTheValidValues_ThrowsInvalidOperationException()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().UpdateStatusAsync(1, "Cancelled", actingUserId: 1));
        }

        [Fact]
        public async Task UpdateStatusAsync_WhenInvoiceDoesNotExist_ReturnsFalse()
        {
            _invoiceRepo.Query().Returns(new List<Invoice>().BuildMock());

            var result = await CreateSut().UpdateStatusAsync(999, "Sent", actingUserId: 1);

            Assert.False(result);
        }

        [Fact]
        public async Task UpdateStatusAsync_WhenTransitioningFromNonPaidToPaid_SetsPaidAtAndLogsMarkedPaid()
        {
            var invoice = new Invoice
            {
                Id = 1, InvoiceNumber = "INV-1", Status = "Sent", PaidAt = null,
                LineItems = new List<InvoiceLineItem> { new() { Quantity = 2, UnitPrice = 50m } }
            };
            _invoiceRepo.Query().Returns(new List<Invoice> { invoice }.BuildMock());

            var result = await CreateSut().UpdateStatusAsync(1, "Paid", actingUserId: 9);

            Assert.True(result);
            Assert.Equal("Paid", invoice.Status);
            Assert.NotNull(invoice.PaidAt);
            await _activityLogService.Received(1).LogAsync("Invoice", 1, "INV-1", 9, "MarkedPaid", Arg.Any<string>());
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task UpdateStatusAsync_WhenMovingOffPaidStatus_ClearsPaidAtAndDoesNotLog()
        {
            var invoice = new Invoice { Id = 1, Status = "Paid", PaidAt = new DateTime(2026, 1, 1) };
            _invoiceRepo.Query().Returns(new List<Invoice> { invoice }.BuildMock());

            var result = await CreateSut().UpdateStatusAsync(1, "Sent", actingUserId: 9);

            Assert.True(result);
            Assert.Null(invoice.PaidAt);
            await _activityLogService.DidNotReceive().LogAsync(
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string?>());
        }

        [Fact]
        public async Task UpdateStatusAsync_WhenAlreadyPaidAndStayingPaid_LeavesPaidAtUnchangedAndDoesNotLogAgain()
        {
            var originalPaidAt = new DateTime(2026, 1, 1);
            var invoice = new Invoice { Id = 1, Status = "Paid", PaidAt = originalPaidAt };
            _invoiceRepo.Query().Returns(new List<Invoice> { invoice }.BuildMock());

            var result = await CreateSut().UpdateStatusAsync(1, "Paid", actingUserId: 9);

            Assert.True(result);
            Assert.Equal(originalPaidAt, invoice.PaidAt);
            await _activityLogService.DidNotReceive().LogAsync(
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string?>());
        }

        [Fact]
        public async Task UpdateStatusAsync_WhenMovingBetweenTwoNonPaidStatuses_LeavesPaidAtNull()
        {
            var invoice = new Invoice { Id = 1, Status = "Draft", PaidAt = null };
            _invoiceRepo.Query().Returns(new List<Invoice> { invoice }.BuildMock());

            var result = await CreateSut().UpdateStatusAsync(1, "Overdue", actingUserId: 9);

            Assert.True(result);
            Assert.Null(invoice.PaidAt);
        }

        // ----- UploadFileAsync -----

        [Fact]
        public async Task UploadFileAsync_WhenInvoiceDoesNotExist_ReturnsNull()
        {
            _invoiceRepo.FindAsync(999).Returns((Invoice?)null);

            var result = await CreateSut().UploadFileAsync(999, FakeFile("doc.pdf"));

            Assert.Null(result);
        }

        [Fact]
        public async Task UploadFileAsync_WhenFirstUploadAndNotCreatedFromATemplate_SuggestsSaveAsTemplateAndWritesTheFileToDisk()
        {
            var invoice = new Invoice { Id = 1, StoredFileName = null, CreatedFromTemplateId = null };
            _invoiceRepo.FindAsync(1).Returns(invoice);

            var result = await CreateSut().UploadFileAsync(1, FakeFile("quote.pdf"));

            Assert.NotNull(result);
            Assert.True(result!.SuggestSaveAsTemplate);
            Assert.Equal("quote.pdf", invoice.OriginalFileName);
            Assert.NotNull(invoice.StoredFileName);
            Assert.EndsWith("_quote.pdf", invoice.StoredFileName);
            Assert.True(File.Exists(Path.Combine(_invoicePath, invoice.StoredFileName!)));
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task UploadFileAsync_WhenInvoiceAlreadyHasAFile_DoesNotSuggestSaveAsTemplateAndDeletesTheOldFileFromDisk()
        {
            const string oldStoredName = "old_file.pdf";
            File.WriteAllText(Path.Combine(_invoicePath, oldStoredName), "old content");
            var invoice = new Invoice { Id = 1, StoredFileName = oldStoredName, OriginalFileName = "old.pdf" };
            _invoiceRepo.FindAsync(1).Returns(invoice);

            var result = await CreateSut().UploadFileAsync(1, FakeFile("new.pdf"));

            Assert.NotNull(result);
            Assert.False(result!.SuggestSaveAsTemplate);
            Assert.False(File.Exists(Path.Combine(_invoicePath, oldStoredName)));
            Assert.NotEqual(oldStoredName, invoice.StoredFileName);
        }

        [Fact]
        public async Task UploadFileAsync_WhenInvoiceWasCreatedFromATemplate_DoesNotSuggestSaveAsTemplateEvenOnFirstUpload()
        {
            var invoice = new Invoice { Id = 1, StoredFileName = null, CreatedFromTemplateId = 5 };
            _invoiceRepo.FindAsync(1).Returns(invoice);

            var result = await CreateSut().UploadFileAsync(1, FakeFile("doc.pdf"));

            Assert.NotNull(result);
            Assert.False(result!.SuggestSaveAsTemplate);
        }

        [Fact]
        public async Task UploadFileAsync_WhenTheClientFileNameContainsPathTraversalSegments_StripsThemAndStaysWithinTheUploadDirectory()
        {
            var invoice = new Invoice { Id = 1 };
            _invoiceRepo.FindAsync(1).Returns(invoice);

            var result = await CreateSut().UploadFileAsync(1, FakeFile("../../../evil.pdf"));

            Assert.NotNull(result);
            Assert.NotNull(invoice.StoredFileName);
            Assert.DoesNotContain("..", invoice.StoredFileName);
            Assert.True(File.Exists(Path.Combine(_invoicePath, invoice.StoredFileName!)));
        }

        // ----- GetTemplatesAsync -----

        [Fact]
        public async Task GetTemplatesAsync_ReturnsMappedTemplates()
        {
            _templateRepo.Query().Returns(new List<InvoiceTemplate>
            {
                new() { Id = 1, Name = "Standard Look", OriginalFileName = "a.pdf", Creator = new User { Name = "Alice" } }
            }.BuildMock());

            var result = await CreateSut().GetTemplatesAsync();

            Assert.Single(result);
            Assert.Equal("Standard Look", result[0].Name);
            Assert.Equal("Alice", result[0].CreatorName);
        }

        // ----- SaveAsTemplateAsync -----

        [Fact]
        public async Task SaveAsTemplateAsync_WhenInvoiceDoesNotExist_ReturnsNull()
        {
            _invoiceRepo.Query().Returns(new List<Invoice>().BuildMock());

            var result = await CreateSut().SaveAsTemplateAsync(999, new SaveAsTemplateDto { Name = "T" }, createdBy: 1);

            Assert.Null(result);
        }

        [Fact]
        public async Task SaveAsTemplateAsync_WhenInvoiceHasNoStoredFile_ReturnsNull()
        {
            var invoice = new Invoice { Id = 1, StoredFileName = null };
            _invoiceRepo.Query().Returns(new List<Invoice> { invoice }.BuildMock());

            var result = await CreateSut().SaveAsTemplateAsync(1, new SaveAsTemplateDto { Name = "T" }, createdBy: 1);

            Assert.Null(result);
        }

        [Fact]
        public async Task SaveAsTemplateAsync_WhenTheSourceFileIsMissingOnDisk_ReturnsNull()
        {
            var invoice = new Invoice { Id = 1, StoredFileName = "does-not-exist.pdf" };
            _invoiceRepo.Query().Returns(new List<Invoice> { invoice }.BuildMock());

            var result = await CreateSut().SaveAsTemplateAsync(1, new SaveAsTemplateDto { Name = "T" }, createdBy: 1);

            Assert.Null(result);
        }

        [Fact]
        public async Task SaveAsTemplateAsync_WhenTheSourceFileExists_CopiesItAndCreatesATemplateDefaultingToTheInvoicesClientName()
        {
            const string storedName = "abc_source.pdf";
            File.WriteAllText(Path.Combine(_invoicePath, storedName), "content");
            var invoice = new Invoice
            {
                Id = 1, StoredFileName = storedName, OriginalFileName = "source.pdf", ClientName = "Acme",
                FileContentType = "application/pdf", FileSize = 7
            };
            _invoiceRepo.Query().Returns(new List<Invoice> { invoice }.BuildMock());

            InvoiceTemplate? added = null;
            _templateRepo.When(r => r.Add(Arg.Any<InvoiceTemplate>())).Do(ci =>
            {
                added = ci.Arg<InvoiceTemplate>();
                added.Id = 10;
                added.Creator = new User { Id = 3, Name = "Bob" };
            });
            _templateRepo.Query().Returns(_ => new List<InvoiceTemplate> { added! }.BuildMock());

            var dto = new SaveAsTemplateDto { Name = "My Template" };
            var result = await CreateSut().SaveAsTemplateAsync(1, dto, createdBy: 3);

            Assert.NotNull(result);
            Assert.Equal("My Template", result!.Name);
            Assert.Equal("Bob", result.CreatorName);
            Assert.Equal("Acme", result.ClientName);
            _templateRepo.Received(1).Add(Arg.Is<InvoiceTemplate>(t => t.Name == "My Template" && t.CreatedBy == 3));
            Assert.Single(Directory.GetFiles(_templatePath));
        }

        [Fact]
        public async Task SaveAsTemplateAsync_WhenDtoProvidesAClientName_UsesThatInsteadOfTheInvoicesClientName()
        {
            const string storedName = "abc_source.pdf";
            File.WriteAllText(Path.Combine(_invoicePath, storedName), "content");
            var invoice = new Invoice { Id = 1, StoredFileName = storedName, OriginalFileName = "source.pdf", ClientName = "Acme" };
            _invoiceRepo.Query().Returns(new List<Invoice> { invoice }.BuildMock());

            InvoiceTemplate? added = null;
            _templateRepo.When(r => r.Add(Arg.Any<InvoiceTemplate>())).Do(ci =>
            {
                added = ci.Arg<InvoiceTemplate>();
                added.Id = 10;
                added.Creator = new User { Name = "Bob" };
            });
            _templateRepo.Query().Returns(_ => new List<InvoiceTemplate> { added! }.BuildMock());

            var result = await CreateSut().SaveAsTemplateAsync(1, new SaveAsTemplateDto { Name = "T", ClientName = "Override Co" }, createdBy: 3);

            Assert.NotNull(result);
            Assert.Equal("Override Co", result!.ClientName);
        }

        // ----- CreateFromTemplateAsync -----

        [Fact]
        public async Task CreateFromTemplateAsync_WhenTemplateDoesNotExist_ReturnsNull()
        {
            _templateRepo.Query().Returns(new List<InvoiceTemplate>().BuildMock());

            var result = await CreateSut().CreateFromTemplateAsync(999, new CreateInvoiceDto(), createdBy: 1);

            Assert.Null(result);
        }

        [Fact]
        public async Task CreateFromTemplateAsync_WhenTheTemplateFileIsMissingOnDisk_CreatesTheInvoiceWithoutFileInfo()
        {
            var template = new InvoiceTemplate
            {
                Id = 5, StoredFileName = "missing.pdf", OriginalFileName = "orig.pdf",
                ContentType = "application/pdf", FileSize = 100
            };
            _templateRepo.Query().Returns(new List<InvoiceTemplate> { template }.BuildMock());

            Invoice? added = null;
            _invoiceRepo.When(r => r.Add(Arg.Any<Invoice>())).Do(ci =>
            {
                added = ci.Arg<Invoice>();
                added.Id = 20;
                added.Creator = new User { Name = "U" };
            });
            _invoiceRepo.Query().Returns(_ => new List<Invoice> { added! }.BuildMock());

            var dto = new CreateInvoiceDto { InvoiceNumber = "INV-2", ClientName = "C", LineItems = new() };
            var result = await CreateSut().CreateFromTemplateAsync(5, dto, createdBy: 9);

            Assert.NotNull(result);
            _invoiceRepo.Received(1).Add(Arg.Is<Invoice>(i =>
                i.CreatedFromTemplateId == 5 && i.StoredFileName == null && i.OriginalFileName == null));
        }

        [Fact]
        public async Task CreateFromTemplateAsync_WhenTheTemplateFileExists_CopiesItAndCarriesTheFileMetadataOntoTheNewInvoice()
        {
            const string storedName = "xyz_template.pdf";
            File.WriteAllText(Path.Combine(_templatePath, storedName), "template content");
            var template = new InvoiceTemplate
            {
                Id = 5, StoredFileName = storedName, OriginalFileName = "template.pdf",
                ContentType = "application/pdf", FileSize = 123
            };
            _templateRepo.Query().Returns(new List<InvoiceTemplate> { template }.BuildMock());

            Invoice? added = null;
            _invoiceRepo.When(r => r.Add(Arg.Any<Invoice>())).Do(ci =>
            {
                added = ci.Arg<Invoice>();
                added.Id = 20;
                added.Creator = new User { Name = "U" };
            });
            _invoiceRepo.Query().Returns(_ => new List<Invoice> { added! }.BuildMock());

            var dto = new CreateInvoiceDto { InvoiceNumber = "INV-3", ClientName = "C", LineItems = new() };
            var result = await CreateSut().CreateFromTemplateAsync(5, dto, createdBy: 9);

            Assert.NotNull(result);
            _invoiceRepo.Received(1).Add(Arg.Is<Invoice>(i =>
                i.CreatedFromTemplateId == 5 &&
                i.OriginalFileName == "template.pdf" &&
                i.FileContentType == "application/pdf" &&
                i.FileSize == 123 &&
                i.StoredFileName != null));
            Assert.Single(Directory.GetFiles(_invoicePath));
        }

        // ----- DeleteTemplateAsync -----

        [Fact]
        public async Task DeleteTemplateAsync_WhenTemplateDoesNotExist_ReturnsFalse()
        {
            _templateRepo.FindAsync(999).Returns((InvoiceTemplate?)null);

            var result = await CreateSut().DeleteTemplateAsync(999);

            Assert.False(result);
        }

        [Fact]
        public async Task DeleteTemplateAsync_WhenTemplateExists_DeletesItsFileFromDiskAndRemovesTheRecord()
        {
            const string storedName = "del_me.pdf";
            File.WriteAllText(Path.Combine(_templatePath, storedName), "x");
            var template = new InvoiceTemplate { Id = 1, StoredFileName = storedName };
            _templateRepo.FindAsync(1).Returns(template);

            var result = await CreateSut().DeleteTemplateAsync(1);

            Assert.True(result);
            Assert.False(File.Exists(Path.Combine(_templatePath, storedName)));
            _templateRepo.Received(1).Remove(template);
        }

        // ----- DownloadFileAsync -----

        [Fact]
        public async Task DownloadFileAsync_WhenInvoiceDoesNotExist_ReturnsNull()
        {
            _invoiceRepo.FindAsync(999).Returns((Invoice?)null);

            var result = await CreateSut().DownloadFileAsync(999);

            Assert.Null(result);
        }

        [Fact]
        public async Task DownloadFileAsync_WhenInvoiceHasNoStoredFile_ReturnsNull()
        {
            var invoice = new Invoice { Id = 1, StoredFileName = null };
            _invoiceRepo.FindAsync(1).Returns(invoice);

            var result = await CreateSut().DownloadFileAsync(1);

            Assert.Null(result);
        }

        [Fact]
        public async Task DownloadFileAsync_WhenTheFileIsMissingOnDisk_ReturnsNull()
        {
            var invoice = new Invoice { Id = 1, StoredFileName = "gone.pdf" };
            _invoiceRepo.FindAsync(1).Returns(invoice);

            var result = await CreateSut().DownloadFileAsync(1);

            Assert.Null(result);
        }

        [Fact]
        public async Task DownloadFileAsync_WhenTheFileExists_ReturnsItsContentAndMetadata()
        {
            const string storedName = "abc_report.pdf";
            File.WriteAllBytes(Path.Combine(_invoicePath, storedName), new byte[] { 1, 2, 3 });
            var invoice = new Invoice { Id = 1, StoredFileName = storedName, OriginalFileName = "report.pdf", FileContentType = "application/pdf" };
            _invoiceRepo.FindAsync(1).Returns(invoice);

            var result = await CreateSut().DownloadFileAsync(1);

            Assert.NotNull(result);
            Assert.Equal(new byte[] { 1, 2, 3 }, result!.Value.Content);
            Assert.Equal("application/pdf", result.Value.ContentType);
            Assert.Equal("report.pdf", result.Value.FileName);
        }
    }
}
