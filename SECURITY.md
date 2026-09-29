# Security

## Reporting

Report vulnerabilities through [GitHub's private advisory form](../../security/advisories/new).
Please do not open a public issue for anything exploitable.

## What to expect

An emulator runs software it did not write and cannot trust. A ROM image that
makes the emulator read or write outside its own memory, hang the host, or
exhaust memory is a security bug, and worth reporting.

The core makes no network requests. The test suite downloads its test data
from pinned commits and checks each file against a recorded hash before use.
