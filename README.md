# SignalBench v0.2.3.2

**A professional-grade telemetry decoding and analysis workbench for satellite, aerospace, automotive, and industrial test engineers.**

SignalBench is a high-performance, engineer-grade telemetry workbench designed for mission-critical test campaigns. It supports everything from CubeSat missions to complex flight test systems, providing robust decoding for CSV, binary logs, and live network or serial streams. Decode, visualize, and analyze telemetry without the need for custom scripting.

## 🏗️ Project Structure

The SignalBench ecosystem is split into four main projects:

- **SignalBench**: The main Avalonia UI application.
- **SignalBench.Core**: The engine responsible for data storage, ingestion, and session management.
- **SignalBench.SDK**: The public bridge for plugin developers. Contains all core interfaces (`ITelemetrySource`, `IPlugin`, `ITabViewModel`).
- **SignalBench.Tests**: Unit and integration tests for decoding and streaming.

## 🛠️ Recent Project Evolution

The project has recently undergone a major architectural overhaul to support a "Core vs. Community" plugin strategy:

- **SDK Extraction**: Extracted `SignalBench.SDK` into a standalone project. Moved all core interfaces (`IPlugin`, `ITelemetrySource`, `IDataStore`) to the SDK to allow third-party developers to build plugins without needing the full source code.
- **Plugin Infrastructure**: Implemented a robust `PluginLoader` service in the Core. The app now dynamically scans a `Plugins/` directory on startup, loading any DLL that implements the `IPlugin` interface.
- **Visualization Refactor**: Refactored the internal "Tab" system to be entirely generic. The UI no longer assumes every tab is a `PlotView`. By implementing `ITabViewModel` and `ITabFactory`, developers can now add entirely new view types (e.g., 3D Models, Maps, Gauges) via plugins.

## 🚀 Features

- **Workspace-Centric UI**: Top-level tabbed architecture. Each tab is a complete workspace with its own independent data source (File, Serial, or Network), signal selection sidebar, and plot configuration.
- **Intelligent Data Import**: Specialized, format-aware dialogs for CSV and Binary data. Includes live data previews, delimiter/header configuration, and validation to prevent malformed imports.
- **Binary Telemetry Mapping**: Full control over binary decoding via YAML-defined packet schemas. Supports custom timestamp field selection from any numeric field in the schema.
- **Live Network Streaming**: Connect via **TCP (Client)** or **UDP (Listener)** to decode and visualize data in real-time.
- **Live Serial Streaming**: Connect to COM ports with full control over Baud Rate, Parity, and Stop Bits.
- **High Performance**: Handles large files (> 500K+ records) and high-frequency streams efficiently using a Hybrid (In-Memory/SQLite) data store.
- **Visualization**: Interactive plots with a **Time-Based Rolling Window** (e.g., show the last 10 seconds of live data).
- **Derived Signals**: Create custom calculated signals using math expressions (e.g., `sqrt(battery_voltage)`).
- **Data Logging**: Record raw network or serial streams directly to disk while visualizing.
- **Session Management**: Save and restore workspace sessions (`.sbs` files). Supports multi-tab persistence, embedded schemas, and **automatic restoration** of the last session on startup.

## 🚀 Getting Started (Quick Start)

### 1. Load a Packet Schema
Before loading binary data or streaming, the app needs to know the structure of the packets.
*   Click **"Create Schema"** or **"Open Schema"** in the **SCHEMA** group.
*   Define or select a `.yaml` schema file.

### 2. Import Static Telemetry Data
*   **CSV Import**: Click the **CSV File icon** in the **DATA FILE** group.
    *   Select your file, configure the **Delimiter** and **Has Header** settings.
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
- **Runtime**: .NET 9.0 SDK
- **Hardware**: Serial port access or Network (Ethernet/WiFi) connectivity
- **Dependencies**: 
  - Avalonia UI
  - ScottPlot (v5.1+)
  - System.IO.Ports
  - YamlDotNet
  - Microsoft.Data.Sqlite
  - NCalcSync

### Building from Source

```bash
dotnet build SignalBench.sln
```

### Running Tests

```bash
dotnet test
```

---

*Built for engineers who need results, not fluff.*
