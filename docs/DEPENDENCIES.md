# Dependencies and attribution

| Component | Source |
| --- | --- |
| NAudio 1.10.0 | [NuGet package](https://www.nuget.org/packages/NAudio/1.10.0); the existing [NAudio license notice](../NAudio-LICENSE.txt) is retained. |
| Microsoft.Windows.SDK.Contracts 10.0.26100.7705 | [NuGet package and license link](https://www.nuget.org/packages/Microsoft.Windows.SDK.Contracts/10.0.26100.7705); Windows Runtime build metadata. |
| Browser tooling | `browser-agent/package-lock.json` pins the dependency tree, including the MCP SDK and Playwright. |
| Voice, speech recognition, model, and game dependencies | Optional. The relevant setup scripts document their providers and retrieve them separately. |

The application build uses the Windows .NET Framework compiler and WPF. Restore downloads the two pinned NuGet packages into the ignored local cache and extracts the files used by the existing build. Downloaded software, data, and models have their own terms. The repository snapshot makes no new project-wide license selection.
