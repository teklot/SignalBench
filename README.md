# SignalBench & SignalFlux v0.3.0

**A professional-grade binary protocol platform for satellite, aerospace, automotive, and industrial telemetry decoding, analysis, and signal generation.**

SignalBench is a high-performance, engineer-grade telemetry workbench designed for mission-critical test campaigns. Supporting everything from CubeSat missions to complex automotive flight test systems, the ecosystem now integrates **SignalFlux** to provide a complete end-to-end solution: generate, simulate, and analyze binary protocols for aerospace and industrial applications using a unified schema-driven engine.

## 🏗️ Project Structure

The ecosystem is split into five main projects:

- **SignalBench**: The main Avalonia UI application for visualization and analysis.
- **SignalFlux**: The high-performance CLI engine for headless signal generation and protocol simulation.
- **SignalBench.Core**: The unified engine responsible for schema-driven encoding/decoding, transport, and data storage.
- **SignalBench.SDK**: The public bridge for plugin developers.
- **SignalBench.Tests**: Unit and integration tests for the unified core.

---

## 📄 DSDL-Lite Packet Schema (Common Foundation)

The foundation of both tools is the **DSDL-Lite** schema (YAML). It provides a "Single Source of Truth" for how binary data is bit-packed, scaled, and interpreted.

### 🛠️ Schema Rules & Specifications

| Feature | Description |
| :--- | :--- |
| **Endianness** | Supports `little` (default) and `big` at the packet level. |
| **Types** | `uint8`, `int8`, `uint16`, `int16`, `uint32`, `int32`, `uint64`, `int64`, `float32`, `float64`, `bool`. |
| **Bit Packing** | Fields can be byte-aligned or bit-packed using `bit_offset` and `bit_length`. |
| **Transformations** | `Physical = (Raw * scale) + offset`. Supports floating-point precision. |
| **Categorical Mapping** | The `lookup` dictionary maps numeric codes to human-readable strings (Enums). |
| **Integrity (CRC)** | Supports `Crc8`, `Crc16`, and `Crc32` with custom polynomials and reflection. |

### Example: `engine_telemetry.yaml`
```yaml
packet:
  name: "EngineData"
  endianness: little
  fields:
    - name: "rpm"
      type: uint16
      unit: "RPM"
      scale: 1.0
    - name: "temp"
      type: int16
      scale: 0.01
      unit: "°C"
      description: "Cylinder Head Temperature"
    - name: "status"
      type: uint8
      lookup:
        0: "OFF"
        1: "IDLE"
        2: "RUNNING"
        3: "OVERHEAT"
  crc:
    type: Crc16
    bit_offset: 40 # Position after 2+2+1 bytes
```

---

## 🖥️ SignalBench (GUI) - Analysis & Visualization

SignalBench is the visual workbench for engineers to monitor and analyze telemetry.

### ✨ GUI Features
- **Workspace-Centric UI**: Independent workspaces per tab with their own data sources and plot configurations.
- **Intelligent Data Import**: specialized, format-aware dialogs for Delimited Text (CSV, TSV, TXT, LOG) and Binary data with live previews.
- **Live Streaming**: Connect via **TCP (Client)**, **UDP (Listener)**, or **Serial** to visualize data in real-time.
- **Visualization**: Interactive plots with a **Time-Based Rolling Window** (e.g., show the last 10 seconds of live data).
- **Derived Signals**: Create custom calculated signals using math expressions (e.g., `sqrt(battery_voltage)`).
- **Integrity Validation**: Visual red "X" markers for packets failing CRC checks. Corrupted data points are visually flagged directly on the signal traces.
- **Data Logging**: Record raw network or serial streams directly to disk while visualizing.
- **Session Management**: Save and restore complete workspace states to `.sbs` files.

### 🚀 SignalBench Quick Start
1.  **Load a Schema**: Click **"Open Schema"** in the **SCHEMA** group and select a `.yaml` file.
2.  **Import Data**: Click the **Text** or **Binary** icons in the **DATA FILE** group to load static logs.
3.  **Start Streaming**: Click the **Network** or **Serial** icons in the **STREAM** group to connect to live hardware.
4.  **Analyze**: Select signals from the sidebar to add them to the plot.

### 🛠️ Threshold & Integrity Monitoring
- **Formula-Based Thresholds**: Use the expression engine to define conditions (e.g., `temp > 85 AND pressure > 120`). Violations are marked with diamond indicators.
- **Violation Export**: Export a complete list of all triggered alerts to a CSV file.

### 🔌 Extensibility & Plugins
- **Standalone SDK**: `SignalBench.SDK` allows building custom plugins without modifying core code.
- **Dynamic Loading**: Automatically loads DLLs from the `Plugins/` directory.
- **Custom Viewports**: Implement `ITabViewModel` to add 3D models, maps, or custom gauges.

---

## ⚡ SignalFlux (CLI) - Generation & Simulation

SignalFlux is a high-performance CLI engine for headless signal generation and protocol simulation.

### ✨ CLI Features
- **Headless Operation**: Ideal for Linux servers, CI/CD pipelines, or automated test benches.
- **Schema-Driven Encoding**: Automatically handles bit-packing, scaling, and endianness based on your YAML definition.
- **Mathematical Signal Generation**: Built-in generators for `sine` and `constant` waves.
- **Protocol Simulation**: Mimic complex sensors, flight controllers, or industrial PLCs.

### 🚀 SignalFlux Quick Start
```bash
# Option A: Run using a configuration file (Recommended)
signalflux --config simulation.conf

# Option B: Run an inline simulation with a schema
signalflux --schema engine.yaml --transport-type udp --target 127.0.0.1:14550 --freq 1.0 --amp 0.5
```

### 🛠️ CLI Parameters & Usage

| Parameter | Alias | Description |
| :--- | :--- | :--- |
| `--config` | `-c` | **Path to the `.conf` file.** If provided, other inline parameters are ignored. |
| `--schema` | `-s` | **Path to a `.yaml` schema file** for inline protocol simulation. |
| `--protocol`| `-p` | Protocol name (e.g., `mavlink`, `can`, `arinc429`). |
| `--message` | `-m` | Message or Group name defined in the schema (e.g., `ATTITUDE`). |
| `--transport-type`| `-t`| Transport protocol: `udp` (default), `tcp`, or `serial`. |
| `--target` | `-g` | Target endpoint (e.g., `127.0.0.1:14550` or `COM3:115200`). |
| `--signal-type`| | Type of signal to generate: `sine` (default) or `constant`. |
| `--freq` | `-f` | Frequency in Hz for sine wave generation. |
| `--amp` | `-a` | Amplitude (or constant value) for the signal. |
| `--schema-dir`| | Directory to search for referenced schemas if not in the config path. |

### 📄 SignalFlux Configuration (.conf) Specification

SignalFlux uses a simple INI-style configuration to orchestrate complex simulations with multiple transports and signals.

#### 1. `[transport.name]` Section
Defines an outgoing binary stream. Each transport can have its own independent update rate.
*   `type`: `udp`, `tcp`, or `serial`.
*   `target`: The destination endpoint (`host:port` or `COMx:baud`).
*   `schema`: The relative path to the DSDL-Lite `.yaml` file defining the packet layout.
*   `tick_ms`: **(Optional)** The interval between simulation steps for this transport (e.g., `20` for 50Hz). Defaults to `10`.

#### 2. `[packet_name]` Section
Maps fields defined in the schema to mathematical signal generators. The section name must match the `name` property inside the YAML schema.
*   **Sine Signal**: `sine(frequency, amplitude, phase, offset)`
    *   Example: `rpm = sine(0.5, 2000, 0, 3000)`
*   **Constant Signal**: `constant(value)` or a raw number.
    *   Example: `status = constant(1)` or `status = 1`

### 📋 Simulation Workflow Example

1.  **Define the Schema**: Create `engine_telemetry.yaml` (as shown in the Schema section).
2.  **Configure Simulation**: Create `simulation.conf`:
    ```ini
    [transport.telemetry]
    type = udp
    target = 127.0.0.1:14550
    schema = engine_telemetry.yaml
    tick_ms = 20  # Independent 50Hz rate
    ```
3.  **Execute**: Run `signalflux --config simulation.conf`.

---

## 📋 Requirements & Setup

- **Platform**: Windows, Linux, macOS (via Avalonia)
- **Runtime**: .NET 10.0 SDK
- **Dependencies**: Avalonia UI, ScottPlot, YamlDotNet, SQLite, NCalc.

### Building & Testing
```bash
# Build the entire solution
dotnet build SignalBench.slnx

# Run all core and CLI tests
dotnet test SignalBench.slnx
```

### Development Notes
- **Central Package Management (CPM)**: All NuGet versions are managed in `Directory.Packages.props`.
- **SDK Portability**: `SignalBench.SDK` uses `VersionOverride` to ensure safe referencing from external projects.

---

*Built for engineers who need results, not fluff.*
