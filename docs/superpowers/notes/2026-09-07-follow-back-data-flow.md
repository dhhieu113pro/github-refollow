# Follow-back data flow

GitHub identity -> complete current Followers -> complete current Following -> case-insensitive set difference -> persistent pending journal -> optional sequential PUT requests -> remove completed entries. Dry run stops before the journal and mutation stage. Status and dashboard read the same current comparison. Historical snapshots are preserved for recovery evidence, not used to create new automatic follow targets.
