namespace KhoiProjectManagement.Application
{
    // Same shape as CreateUserDto minus Password - an admin-created account always gets a
    // server-generated temp password (see UserService.CreateUserWithTempPasswordAsync), never one the
    // admin types in on the new user's behalf.
    public class CreateAdminUserDto
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = "member";
        public string Position { get; set; } = string.Empty;
        public int? ManagerId { get; set; }

        // Optional - lets HR capture a new employee's birthday at creation time so it shows up on
        // the company Calendar immediately, instead of only ever being set later via the separate
        // date-of-birth endpoint.
        public DateTime? DateOfBirth { get; set; }
    }
}
