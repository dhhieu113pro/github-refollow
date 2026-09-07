# No-unfollow requirement

The approved follow-back operation must never remove an existing follow. The old DELETE -> delay -> PUT loop is replaced by PUT-only processing of accounts that are current followers and absent from current Following. Existing follows, including accounts outside the Followers list, are not changed. Do not run a live account operation as part of implementation verification.
