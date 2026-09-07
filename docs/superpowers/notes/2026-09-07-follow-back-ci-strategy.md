# CI strategy

The local execution environment lacks the .NET SDK and cannot access github.com. Use the GitHub connector to create an isolated branch, add regression tests, and let the existing GitHub Actions workflow run the .NET 10 build and tests. Implement the approved fix, validate the green CI result, and open the pull request. Do not use live account credentials or perform account mutations for testing.
