namespace KhoiProjectManagement.Application
{
    // Plain "@Full Name" text parsing, matched against real user names - deliberately not a
    // structured mention picker (no new autocomplete UI), per the user's explicit choice. Matches the
    // longest names first so "@Kenneth Mothobi" doesn't also register a false partial match if a
    // shorter "Kenneth" existed as someone else's full name.
    public static class MentionParser
    {
        public static List<int> FindMentionedUserIds(string body, IEnumerable<(int Id, string Name)> candidates, int excludeUserId)
        {
            if (string.IsNullOrWhiteSpace(body))
                return new List<int>();

            var mentioned = new List<int>();
            foreach (var (id, name) in candidates.OrderByDescending(c => c.Name.Length))
            {
                if (id == excludeUserId || string.IsNullOrWhiteSpace(name))
                    continue;

                if (body.Contains("@" + name, StringComparison.OrdinalIgnoreCase))
                    mentioned.Add(id);
            }

            return mentioned;
        }
    }
}
