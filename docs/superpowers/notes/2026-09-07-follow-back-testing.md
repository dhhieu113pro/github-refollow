# Follow-back testing scope

Regression tests must demonstrate the old behavior fails for a follower absent from Following, then pass with the new implementation. Verify the 17/3 scenario, pagination, case normalization, zero-missing no-op, dry run, no DELETE requests, pending recovery filtering, partial follow failures, retry, and API status reporting. Run the full .NET 10 suite and CI container checks.
