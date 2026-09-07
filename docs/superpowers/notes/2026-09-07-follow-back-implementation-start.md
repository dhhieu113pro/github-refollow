# Implementation start

The investigation and approved design are complete. The actual code change is to add Followers retrieval and replace the existing destructive re-follow loop with current Followers minus Following, processed through PUT-only calls. Test the 17/3 scenario and all safety invariants, update the API/dashboard, and verify CI before creating a PR. No live account mutations are part of this work.
