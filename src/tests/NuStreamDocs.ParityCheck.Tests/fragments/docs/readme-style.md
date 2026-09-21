# ProjectName

[![Build](https://img.shields.io/badge/build-passing-green.svg)](https://example.com/build) [![NuGet](https://img.shields.io/nuget/v/Project.svg)](https://www.nuget.org/packages/Project)

A short description of the project that explains **what** it does and *why* it exists. See the [documentation](https://example.com/docs) for details.

## Table of Contents

- [Installation](#installation)
- [Usage](#usage)
    - [Basic usage](#basic-usage)
    - [Advanced usage](#advanced-usage)
- [Contributing](#contributing)
- [License](#license)

## Installation

Install from NuGet:

```bash
dotnet add package ProjectName
```

Or add a reference by hand:

```xml
<PackageReference Include="ProjectName" Version="1.0.0" />
```

## Usage

### Basic usage

1. Create a builder with `new Builder()`.
2. Configure it with `WithOption()`.
3. Build the result and check `result.IsSuccess`.

```csharp
var builder = new Builder();
builder.WithOption("value");
```

### Advanced usage

Some features need explanation:

* **Caching** - results are cached per key.
* **Retries** - transient failures retry up to 3 times.
* **Logging** - pass an `ILogger` to see diagnostics.

> **Note**
> Advanced features are opt-in and may change between minor versions.

## Contributing

Contributions are welcome! Please read the [contributing guide](CONTRIBUTING.md) first, then:

1. Fork the repository
2. Create a feature branch
3. Open a pull request

Report bugs by [opening an issue](https://github.com/example/project/issues/new).

---

## License

Released under the MIT license. See [LICENSE](LICENSE) for details.

Copyright &copy; 2026 Example Authors &mdash; all rights reserved.
