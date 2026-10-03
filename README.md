# CasaCreta

A **Grasshopper plugin** (`.gha`) for Rhino, built with C# / .NET, powering
the computational design and G-code generation pipeline for the
**DurBeen ClayBot Mega** large-format clay 3D printer.

**Canonical structure:** See [`FINAL_TREE.md`](FINAL_TREE.md) for the
authoritative path and ownership contract.

## Project Layout

```
development/                   All source code and dev artifacts
├── CasaCreta.slnx             .NET solution
├── CasaCreta/                 Compiled Grasshopper plugin (.gha)
│   ├── GCode/                 Curve ordering for G-code generation
│   ├── GHSync/                Live file-sync engine
│   ├── EtoForms/              Eto UI components
│   ├── MeshEdit/              Mesh repair operations
│   └── Surface/               UV curve components
├── scripts/                   Standalone paste-ready GH scripts (C# & Python)
├── definitions/               .gh/.ghx host definitions
└── documentation/             Handbooks, machine docs, research

production/                    Built and validated outputs
├── releases/<version>/        Immutable versioned .gha builds
├── current/                   Active release pointer
└── previous/                  Rollback pointer

configuration/                 Non-secret machine and G-code settings
├── machine/                   DurBeen printer parameters
└── gcode/                     Marlin generation settings

assets/                        Static resources (screenshots, icons)
tools/                         Build and deployment utilities
```

## Build

```bash
cd development
dotnet restore
dotnet build
```

The output `CasaCreta.gha` is placed in `development/CasaCreta/bin/` and can
be loaded into Grasshopper.

## Target Frameworks

- `net7.0-windows`
- `net7.0`
- `net48`

## License

_To be defined._
