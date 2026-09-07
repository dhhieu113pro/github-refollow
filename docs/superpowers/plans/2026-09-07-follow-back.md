# Safe follow-back implementation plan

1. Add paginated Followers retrieval and tests for multiple pages and API failures.
2. Replace unfollow/re-follow execution with a case-insensitive difference of current followers and following, preserving only valid pending followers. Add red/green tests for 17 followers, three missing, dry run, no mutations on failure, idempotence, and recovery.
3. Update recovery status and manual queue to derive targets from current followers and disallow non-follower recovery entries. Preserve compatible API fields and persistent snapshot formats.
4. Update dashboard, documentation, and endpoint tests for follower counts and missing usernames.
5. Run full restore, Release build, tests, and container validation. Create a PR; do not perform live account mutations or merge before verification.
