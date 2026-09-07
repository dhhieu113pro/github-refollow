# Idempotence

A second run with unchanged follower and following lists must produce zero new follow operations after the first succeeds. Existing follows are never removed or re-created. Pending entries are reconciled against current GitHub lists, and duplicate or differently cased logins are processed only once. The service must not assume that the account's number of followers equals its number of following.
