// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules;
using Xunit;

namespace GauntletCI.Tests.Rules;

public class GCI0053Tests
{
    [Fact]
    public async Task LockfileOnlyChange_ShouldFire()
    {
        var orchestrator = RuleOrchestrator.CreateDefault();
        var diff = DiffParser.Parse("""
        diff --git a/package-lock.json b/package-lock.json
        index abc..def 100644
        --- a/package-lock.json
        +++ b/package-lock.json
        @@ -1,5 +1,5 @@
        -    "lodash": "4.17.20"
        +    "lodash": "4.17.21"
        """);

        var result = await orchestrator.RunAsync(diff);
        Assert.True(result.Findings.Any(f => f.RuleId == "GCI0053"),
            "Should flag lockfile change without source code changes");
    }

    [Fact]
    public async Task LockfileWithSourceChange_ShouldNotFire()
    {
        var orchestrator = RuleOrchestrator.CreateDefault();
        var diff = DiffParser.Parse("""
        diff --git a/package-lock.json b/package-lock.json
        index abc..def 100644
        --- a/package-lock.json
        +++ b/package-lock.json
        @@ -1,5 +1,5 @@
        -    "lodash": "4.17.20"
        +    "lodash": "4.17.21"
        diff --git a/src/app.ts b/src/app.ts
        index abc..def 100644
        --- a/src/app.ts
        +++ b/src/app.ts
        @@ -10,5 +10,6 @@
         import lodash from 'lodash';
        +console.log(lodash.version);
        """);

        var result = await orchestrator.RunAsync(diff);
        Assert.True(!result.Findings.Any(f => f.RuleId == "GCI0053"),
            "Should not fire when source code is changed alongside lockfile");
    }

    [Fact]
    public async Task PackagesLockJsonChange_ShouldFire()
    {
        var orchestrator = RuleOrchestrator.CreateDefault();
        var diff = DiffParser.Parse("""
        diff --git a/packages.lock.json b/packages.lock.json
        index abc..def 100644
        --- a/packages.lock.json
        +++ b/packages.lock.json
        @@ -1,5 +1,5 @@
        -    "Newtonsoft.Json": "12.0.1"
        +    "Newtonsoft.Json": "12.0.2"
        """);

        var result = await orchestrator.RunAsync(diff);
        Assert.True(result.Findings.Any(f => f.RuleId == "GCI0053"),
            "Should flag .NET packages.lock.json changes without source changes");
    }

    [Fact]
    public async Task YarnLockChange_ShouldFire()
    {
        var orchestrator = RuleOrchestrator.CreateDefault();
        var diff = DiffParser.Parse("""
        diff --git a/yarn.lock b/yarn.lock
        index abc..def 100644
        --- a/yarn.lock
        +++ b/yarn.lock
        @@ -100,5 +100,5 @@
        -react@17.0.1:
        +react@17.0.2:
           version "17.0.1"
        """);

        var result = await orchestrator.RunAsync(diff);
        Assert.True(result.Findings.Any(f => f.RuleId == "GCI0053"),
            "Should flag yarn.lock changes without source changes");
    }

    [Fact]
    public async Task CargoLockChange_ShouldFire()
    {
        var orchestrator = RuleOrchestrator.CreateDefault();
        var diff = DiffParser.Parse("""
        diff --git a/Cargo.lock b/Cargo.lock
        index abc..def 100644
        --- a/Cargo.lock
        +++ b/Cargo.lock
        @@ -50,5 +50,5 @@
        -version = "0.3.0"
        +version = "0.3.1"
          name = "serde"
        """);

        var result = await orchestrator.RunAsync(diff);
        Assert.True(result.Findings.Any(f => f.RuleId == "GCI0053"),
            "Should flag Cargo.lock changes without source changes");
    }

    [Fact]
    public async Task PythonPipfileLock_ShouldFire()
    {
        var orchestrator = RuleOrchestrator.CreateDefault();
        var diff = DiffParser.Parse("""
        diff --git a/Pipfile.lock b/Pipfile.lock
        index abc..def 100644
        --- a/Pipfile.lock
        +++ b/Pipfile.lock
        @@ -20,5 +20,5 @@
        -        "version": "==2.0.1"
        +        "version": "==2.0.2"
          },
        """);

        var result = await orchestrator.RunAsync(diff);
        Assert.True(result.Findings.Any(f => f.RuleId == "GCI0053"),
            "Should flag Pipfile.lock changes without source changes");
    }

    [Fact]
    public async Task GoModSum_ShouldFire()
    {
        var orchestrator = RuleOrchestrator.CreateDefault();
        var diff = DiffParser.Parse("""
        diff --git a/go.sum b/go.sum
        index abc..def 100644
        --- a/go.sum
        +++ b/go.sum
        @@ -1,3 +1,3 @@
        -github.com/some/lib v1.0.0 h1:abc...
        +github.com/some/lib v1.1.0 h1:def...
          github.com/other/lib v0.1.0 h1:ghi...
        """);

        var result = await orchestrator.RunAsync(diff);
        Assert.True(result.Findings.Any(f => f.RuleId == "GCI0053"),
            "Should flag go.sum changes without source changes");
    }

    [Fact]
    public async Task PoetryLockChange_ShouldFire()
    {
        var orchestrator = RuleOrchestrator.CreateDefault();
        var diff = DiffParser.Parse("""
        diff --git a/poetry.lock b/poetry.lock
        index abc..def 100644
        --- a/poetry.lock
        +++ b/poetry.lock
        @@ -30,5 +30,5 @@
        -version = "1.2.0"
        +version = "1.2.1"
          name = "flask"
        """);

        var result = await orchestrator.RunAsync(diff);
        Assert.True(result.Findings.Any(f => f.RuleId == "GCI0053"),
            "Should flag poetry.lock changes without source changes");
    }

    [Fact]
    public async Task PnpmLockYaml_ShouldFire()
    {
        var orchestrator = RuleOrchestrator.CreateDefault();
        var diff = DiffParser.Parse("""
        diff --git a/pnpm-lock.yaml b/pnpm-lock.yaml
        index abc..def 100644
        --- a/pnpm-lock.yaml
        +++ b/pnpm-lock.yaml
        @@ -10,5 +10,5 @@
        -    express: 4.17.1
        +    express: 4.18.0
          lockfileVersion: 5.1
        """);

        var result = await orchestrator.RunAsync(diff);
        Assert.True(result.Findings.Any(f => f.RuleId == "GCI0053"),
            "Should flag pnpm-lock.yaml changes without source changes");
    }

    [Fact]
    public async Task LockfileWithCSharpChange_ShouldNotFire()
    {
        var orchestrator = RuleOrchestrator.CreateDefault();
        var diff = DiffParser.Parse("""
        diff --git a/packages.lock.json b/packages.lock.json
        index abc..def 100644
        --- a/packages.lock.json
        +++ b/packages.lock.json
        @@ -1,5 +1,5 @@
        -    "Newtonsoft.Json": "12.0.1"
        +    "Newtonsoft.Json": "12.0.2"
        diff --git a/src/Models/Data.cs b/src/Models/Data.cs
        index abc..def 100644
        --- a/src/Models/Data.cs
        +++ b/src/Models/Data.cs
        @@ -5,5 +5,6 @@
          public class Data
          {
        +    public string NewField { get; set; }
          }
        """);

        var result = await orchestrator.RunAsync(diff);
        Assert.True(!result.Findings.Any(f => f.RuleId == "GCI0053"),
            "Should not fire when C# source changes accompany lockfile changes");
    }

    [Fact]
    public async Task LockfileWithJavaScriptChange_ShouldNotFire()
    {
        var orchestrator = RuleOrchestrator.CreateDefault();
        var diff = DiffParser.Parse("""
        diff --git a/package-lock.json b/package-lock.json
        index abc..def 100644
        --- a/package-lock.json
        +++ b/package-lock.json
        @@ -1,5 +1,5 @@
        -    "express": "4.17.1"
        +    "express": "4.18.0"
        diff --git a/src/server.js b/src/server.js
        index abc..def 100644
        --- a/src/server.js
        +++ b/src/server.js
        @@ -5,5 +5,6 @@
          const app = express();
        +app.use(express.json());
        """);

        var result = await orchestrator.RunAsync(diff);
        Assert.True(!result.Findings.Any(f => f.RuleId == "GCI0053"),
            "Should not fire when source code changes accompany lockfile");
    }

    [Fact]
    public async Task LockfileWithGoSourceChange_ShouldNotFire()
    {
        var orchestrator = RuleOrchestrator.CreateDefault();
        var diff = DiffParser.Parse("""
        diff --git a/go.sum b/go.sum
        index abc..def 100644
        --- a/go.sum
        +++ b/go.sum
        @@ -1,3 +1,3 @@
        -github.com/sirupsen/logrus v1.7.0 h1:abc...
        +github.com/sirupsen/logrus v1.8.0 h1:def...
        diff --git a/main.go b/main.go
        index abc..def 100644
        --- a/main.go
        +++ b/main.go
        @@ -10,5 +10,6 @@
          import log "github.com/sirupsen/logrus"
        +log.Info("Updated")
        """);

        var result = await orchestrator.RunAsync(diff);
        Assert.True(!result.Findings.Any(f => f.RuleId == "GCI0053"),
            "Should not fire when Go source changes accompany lockfile");
    }

    [Fact]
    public async Task NoFiles_ShouldNotFire()
    {
        var orchestrator = RuleOrchestrator.CreateDefault();
        var diff = DiffParser.Parse("""
        diff --git a/src/empty.cs b/src/empty.cs
        index abc..def 100644
        --- a/src/empty.cs
        +++ b/src/empty.cs
        """);

        var result = await orchestrator.RunAsync(diff);
        Assert.True(!result.Findings.Any(f => f.RuleId == "GCI0053"),
            "Should not fire when there are no lockfile changes");
    }

    [Fact]
    public async Task MultipleSourceExtensions_ShouldNotFire()
    {
        var orchestrator = RuleOrchestrator.CreateDefault();
        var diff = DiffParser.Parse("""
        diff --git a/package-lock.json b/package-lock.json
        index abc..def 100644
        --- a/package-lock.json
        +++ b/package-lock.json
        @@ -1,5 +1,5 @@
        -    "typescript": "4.5.0"
        +    "typescript": "4.6.0"
        diff --git a/src/app.ts b/src/app.ts
        index abc..def 100644
        --- a/src/app.ts
        +++ b/src/app.ts
        @@ -1,3 +1,4 @@
        +import { version } from 'typescript';
          console.log('Hello');
        diff --git a/lib/utils.py b/lib/utils.py
        index abc..def 100644
        --- a/lib/utils.py
        +++ b/lib/utils.py
        @@ -1,3 +1,4 @@
        +def new_function():
          pass
        """);

        var result = await orchestrator.RunAsync(diff);
        Assert.True(!result.Findings.Any(f => f.RuleId == "GCI0053"),
            "Should not fire when multiple source languages change with lockfile");
    }
}
