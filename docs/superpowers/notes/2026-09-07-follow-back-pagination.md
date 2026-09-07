# Pagination

Both Followers and Following must be fetched to completion using GitHub's Link rel=next pagination and per_page=100. A partial or failed list retrieval must not be treated as a complete set. The authenticated account is verified first; the comparison is case-insensitive and excludes the owner. Only the resulting missing follower set can be passed to the live follow executor.
