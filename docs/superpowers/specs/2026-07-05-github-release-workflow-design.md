# GitHub Release Workflow Design

## Goal

Publish a portable Windows x64 build of SonicRelay Windows Publisher to GitHub Releases from `v*` tags or a manual dispatch, without requiring administrator privileges or repository secrets.

## Approaches considered

1. **Portable self-contained ZIP (selected):** publish `win-x64` as a self-contained unpackaged WinUI app, zip the publish directory, and create the release with GitHub CLI. This minimizes user prerequisites and preserves the existing non-admin runtime model.
2. **Framework-dependent ZIP:** smaller download, but requires users to install a matching .NET runtime and weakens portability.
3. **MSIX/installer:** better installation UX, but signing and per-user elevation behavior add complexity that is outside this issue.

## Workflow

`.github/workflows/release.yml` runs on `windows-latest` for pushed tags matching `v*` and `workflow_dispatch`. It checks out the repository, installs the SDK from `global.json`, restores, builds in Release, runs the repository structure checks and solution tests, then publishes `src/SonicRelay.Windows.App` for `win-x64` with `--self-contained true`.

PowerShell derives the release tag from `github.ref_name` for tag builds or `v0.0.0-manual.<run_number>` for manual builds. It writes build metadata (`version`, commit SHA, runtime) into the publish directory, creates `SonicRelay.WindowsPublisher-win-x64-<version>.zip`, and exposes the tag and artifact path to later steps.

The workflow grants only `contents: write` and uses the runner-provided `GITHUB_TOKEN` through `GH_TOKEN`. `gh release create` creates the release and generates notes; `--verify-tag` is used for tag-triggered releases, while manual releases create a synthetic tag at the dispatched commit.

## Error handling and safety

Every build, test, publish, archive, or release command is fail-fast. Release publication cannot run if tests fail. The artifact is an unpackaged portable ZIP and does not install services, drivers, machine-wide dependencies, registry values, or protected-path data.

## Verification

`tests/Repository.Structure.Tests.ps1` validates the workflow contract: triggers, Windows runner, permissions, restore/build/test ordering inputs, self-contained `win-x64` publish, portable ZIP naming, metadata, and release creation. The focused structure test is run red before creating the workflow and green afterward. A YAML parse check and focused `dotnet publish` validate syntax and publishability without running the full test suite locally.
