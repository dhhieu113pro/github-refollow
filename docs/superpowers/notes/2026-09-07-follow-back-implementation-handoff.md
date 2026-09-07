# Implementation handoff

The user requested and approved the followers-based follow-back fix. Root cause and source code have been inspected. The next action is code implementation, not further investigation: create a branch, add failing tests, implement the paginated follower comparison and PUT-only executor, update recovery/status/UI, run tests and CI, and open a PR. Preserve existing follow relationships and do not perform live account mutations.
