# Pull Request

## Summary
Describe the change and the motivation.

## Related issues
Closes #

## Verification
- [ ] `dotnet build Dorado.sln -c Release /warnaserror` passes with zero warnings
- [ ] `dotnet test Dorado.sln` passes
- [ ] Design-invariants audit passes (see `scripts/mcp_tools.py`)

## Checklist
- [ ] No plaintext secrets, credentials, or tokens introduced
- [ ] No new dangling references to the old `Heretek-AI` organization
- [ ] Documentation updated where behavior or setup changed
