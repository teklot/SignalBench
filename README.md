# SignalBench v0.2.6

**A professional-grade telemetry decoding and analysis workbench for satellite, aerospace, automotive, and industrial test engineers.**

SignalBench is a high-performance, engineer-grade telemetry workbench designed for mission-critical test campaigns. It supports everything from CubeSat missions to complex flight test systems, providing robust decoding for delimited text files (e.g., CSV, TSV, TXT, LOG), binary logs, and live network or serial streams. Decode, visualize, and analyze telemetry without the need for custom scripting.

## 🏗️ Project Structure

The SignalBench ecosystem is split into four main projects:

- **SignalBench**: The main Avalonia UI application.
- **SignalBench.Core**: The engine responsible for data storage, ingestion, and session management.
- **SignalBench.SDK**: The public bridge for plugin developers. Contains all core interfaces (`ITelemetrySource`, `IPlugin`, `ITabViewModel`).
- **SignalBench.Tests**: Unit and integration tests for decoding and streaming.

## 🔌 Extensibility & Plugins

SignalBench is designed with a "Core vs. Community" strategy, allowing engineers to extend the platform without modifying the core source code.

- **Standalone SDK**: `SignalBench.SDK` provides all core interfaces (`IPlugin`, `ITelemetrySource`, `ITabViewModel`, etc.). Third-party developers can build plugins against this DLL.
- **Dynamic Loading**: The application scans a `Plugins/` directory on startup and automatically loads any compatible plugins.
- **Custom Viewports**: The internal "Tab" system is generic. Developers can implement `ITabViewModel` and `ITabFactory` to add entirely new visualization types like 3D models, custom gauges, or maps.
- **Custom Data Sources**: Add support for proprietary telemetry protocols by implementing `ITelemetrySource` via a plugin.

## ✨ Features

- **Workspace-Centric UI**: Top-level tabbed architecture. Each tab is a complete workspace with its own independent data source (File, Serial, or Network), signal selection sidebar, and plot configuration.
- **Intelligent Data Import**: Specialized, format-aware dialogs for Delimited Text (CSV, TSV, TXT, LOG) and Binary data. Includes live data previews, custom delimiter/header configuration, and validation.
- **DSDL-Lite Binary Decoding**: Full control over binary decoding via YAML-defined packet schemas with support for scaling, units, and nested fields. Includes **CRC (Cyclic Redundancy Check)** validation.
- **Live Network Streaming**: Connect via **TCP (Client)** or **UDP (Listener)** to decode and visualize data in real-time.
- **Live Serial Streaming**: Connect to COM ports with full control over Baud Rate, Parity, and Stop Bits.
- **High Performance**: Handles large files (> 500K+ records) and high-frequency streams efficiently using a Hybrid (In-Memory/SQLite) data store.
- **Visualization**: Interactive plots with a **Time-Based Rolling Window** (e.g., show the last 10 seconds of live data).
- **Derived Signals**: Create custom calculated signals using math expressions (e.g., `sqrt(battery_voltage)`).
- **Threshold Monitoring**: Define custom rules using math formulas (e.g., `battery_voltage < 6.5`) to automatically highlight violations with diamond-shaped markers directly on the signal graph.
- **Integrity Checks (CRC)**: Automatically validates incoming binary packets using CRC-8, CRC-16, or CRC-32. Corrupted data points are visually flagged with **red "X" markers** for easy identification.
- **Data Logging**: Record raw network or serial streams directly to disk while visualizing.
- **Session Management**: Save and restore workspace sessions (`.sbs` files). Supports multi-tab persistence and **automatic restoration** of the last session on startup. Session files now use **external schema references** for consistent management.

## 🛠️ Threshold & Integrity Monitoring

New in v0.2.6, SignalBench adds advanced integrity validation alongside existing threshold monitoring.

- **Formula-Based Thresholds**: Use the built-in expression engine to define complex conditions (e.g., `temp > 85 AND pressure > 120`). Violations are marked with diamond indicators.
- **CRC Validation**: Every binary packet is verified against its schema-defined CRC. Support for custom polynomials, initial values, final XOR, and bit reflection.
- **Integrity Flagging**: All data points that fail CRC validation are marked with a **red "X"** directly on the signal traces, allowing engineers to correlate data corruption with environmental or mission events.
- **Violation Export**: Export a complete list of all triggered alerts to a CSV file for post-mission reporting.

## 📄 DSDL-Lite Packet Schema

SignalBench uses a professional-grade YAML schema format inspired by industry standards like DSDL (Data Structure Definition Language). This allows you to define complex binary packets with support for nested groups, linear transformations, and categorical mappings.

### Key Capabilities
- **Recursive Namespacing**: Group related signals into hierarchies (e.g., `Battery/Cell1/Voltage`).
- **Linear Transformation**: Automatically convert raw integers to physical values using `scale` and `offset` (`PhysicalValue = (Raw * Scale) + Offset`).
- **Categorical Lookup**: Map numeric status codes to human-readable strings (Enums).
- **Physical Metadata**: Assign units (V, A, Hz, °C) that are displayed throughout the UI and statistics panels.

### Comprehensive Schema Example
```yaml
packet:
  name: "FlightController"
  endianness: little
  fields:
    - name: "system_status"
      type: uint8
      lookup:
        0: "BOOTING"
        1: "READY"
        2: "FAULT"
        
    - name: "battery"
      fields: # Nested Group
        - name: "voltage"
          type: uint16
          scale: 0.001  # Convert mV to V
          unit: "V"
        - name: "current"
          type: int16
          scale: 0.1
          unit: "A"

    - name: "environment"
      fields:
        - name: "temperature"
          type: int16
          scale: 0.01
          offset: -40.0
          unit: "°C"
          description: "Internal ambient temperature"
          
  # CRC integrity check (Optional)
  crc:
    type: Crc16
    polynomial: 0x1021
    initial_value: 0x0000
    bit_offset: 64 # Where the CRC field is in the packet
    bit_length: 16
    reflect_input: true
    reflect_output: true
```

## 🚀 Getting Started (Quick Start)

### 1. Load a Packet Schema
Before loading binary data or streaming, the app needs to know the structure of the packets.
*   Click **"Create Schema"** or **"Open Schema"** in the **SCHEMA** group.
*   Define or select a `.yaml` schema file.

### 2. Import Static Telemetry Data
*   **Delimited Text File Import**: Click the **Text File icon** in the **DATA FILE** group.
    *   Select your file, configure the **Delimiter** (Comma, Semicolon, Pipe, Tab, etc.) and **Has Header** settings.
    *   The preview grid will update automatically to validate your settings.
*   **Binary Import**: Click the **Binary File icon** (identified by the '101' badge).
    *   Select your file and a corresponding schema.
    *   Optionally select which schema field to use as the **Timestamp Column**.

### 3. Live Streaming (Serial or Network)
Streaming settings are **per-workspace (per-tab)**. You can stream from multiple different sources simultaneously in different tabs.

#### Serial Port
*   Click the **Serial icon** in the **STREAM** group to open configuration.
    *   Select your **COM Port**, **Baud Rate**, and parameters.
    *   Click **Connect** to start the stream.

#### Network (TCP/UDP)
*   Click the **Network icon** in the **STREAM** group.
    *   **Protocol**: Choose **UDP** (listens on a local port) or **TCP** (connects to a server).
    *   Click **Connect** to start the stream.

### 4. Session Management
*   **Save**: Save your entire workspace (all tabs, signals, and schemas) to a `.sbs` file using the **SESSION** group.
*   **Auto-Load**: SignalBench automatically saves your progress and can restore your most recent session on startup.

## 📋 Requirements & Setup

- **Platform**: Windows, Linux, macOS (Cross-platform via Avalonia)
- **Runtime**: .NET 10.0 SDK
- **IDE**: Visual Studio 2026 (or JetBrains Rider / VS Code with .NET 10 support)
- **Dependencies**: 
  - Avalonia UI (v11.3+)
  - ScottPlot (v5.1+)
  - YamlDotNet
  - Microsoft.Data.Sqlite
  - NCalc

### Building from Source

The project uses the modern .NET 10 artifacts layout. Build results are centralized in the `artifacts/` directory at the root of the solution.

```bash
dotnet build SignalBench.slnx
```

### Running Tests

```bash
dotnet test
```

### Development Notes

- **Central Package Management (CPM)**: All NuGet package versions are managed centrally in `Directory.Packages.props`.
- **SDK Portability**: The `SignalBench.SDK` project uses `VersionOverride` for its dependencies to ensure it can be safely referenced as a source project from external solutions.

---

*Built for engineers who need results, not fluff.*
