# Follow-back safety invariants

- Do not call DELETE /user/following/{login}.
- Do not follow any account merely because it appears in an old snapshot.
- Verify the authenticated identity and complete both paginated lists before changes.
- Compute missing accounts case-insensitively and exclude self.
- Preserve an unfinished journal on failure; reconcile it against fresh lists on restart.
- Dry runs never mutate GitHub.
- Do not use or disclose any previously exposed personal access token.
