# Follow-back design

## Goal
Follow back the authenticated account's current followers without unfollowing anyone. Compute the actual missing accounts on every run; do not hard-code follower counts.

## Flow
Read the authenticated login, then fetch complete paginated Followers and Following lists. Normalize and deduplicate login names case-insensitively, excluding the authenticated account. The work set is Followers minus Following. Retain pending recovery entries only while they remain current followers and remain absent from Following. Never restore arbitrary historical following snapshots as new follow targets.

Dry runs report the work set without mutations. Live runs persist the work set before processing it, perform only PUT follow requests sequentially with the configured delay, and remove each completed account from the journal. On interruption, the next run recomputes the current difference before resuming. GitHub API failures stop processing and preserve unfinished work.

## Compatibility and UI
Keep existing configuration, schedule, API routes, and persistent data. Expose follower count, already-followed count, missing count, and missing usernames in status. Keep old following-count fields where necessary for compatibility. Update the dashboard and README to explain follow-back behavior, and ensure manual recovery cannot queue accounts that are no longer followers.

## Verification
Add tests for 17 followers with 3 missing, case-insensitive comparison, pagination, no-op and dry-run behavior, no DELETE requests, recovery filtering, interruption and retry, failure safety, and endpoint reporting. Run the complete .NET test suite and container CI before merge. No live GitHub account mutations are part of this change.
