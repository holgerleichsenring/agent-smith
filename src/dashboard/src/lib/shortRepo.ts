// 2026-10-16-7ed3b: a repository's short name for a tight place — after the last '/', then the last '.' segment
// ("org/Sample.App.Server" -> "Server"). Two names may shorten alike; callers keep the full name as a title.

export function shortRepo(repo: string): string {
  const name = repo.slice(repo.lastIndexOf("/") + 1);
  const short = name.slice(name.lastIndexOf(".") + 1);
  return short || name || repo;
}
