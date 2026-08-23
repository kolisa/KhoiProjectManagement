using KhoiProjectManagement.Domain;

namespace KhoiProjectManagement.Application.Abstractions
{
    // A deliberate, narrow escape hatch from the generic IRepository<T> pattern: Postgres full-text
    // search (EF.Functions.ToTsVector/PlainToTsQuery) is provider-specific and only resolvable against
    // the concrete Npgsql provider, which Application intentionally never references (see IRepository<T>
    // for the general case). Everything downstream of the candidate fetch - permission filtering,
    // snippet building - stays in WikiService like every other business rule.
    public interface IWikiSearchRepository
    {
        Task<List<WikiPage>> FindCandidatesAsync(string query, int take);
    }
}
