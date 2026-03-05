# SignalBench v0.2.3.2

**A professional-grade telemetry decoding and analysis workbench for satellite, aerospace, automotive, and industrial test engineers.**

SignalBench is a high-performance, engineer-grade telemetry workbench designed for mission-critical test campaigns. It supports everything from CubeSat missions to complex flight test systems, providing robust decoding for CSV, binary logs, and live network or serial streams. Decode, visualize, and analyze telemetry without the need for custom scripting.

## 🚀 Features

- **Multiple Format Support**: Load and decode CSV files or binary telemetry using YAML-defined packet schemas.
- **Live Network Streaming**: Connect via **TCP (Client)** or **UDP (Listener)** to decode and visualize data in real-time.
- **Live Serial Streaming**: Connect to COM ports with full control over Baud Rate, Parity, and Stop Bits.
- **Independent Tab Architecture**: Each plot tab maintains its own independent data source (File, Serial, or Network) and configuration.
- **High Performance**: Handles large files (> 500K+ records) and high-frequency streams efficiently using a Hybrid (In-Memory/SQLite) data store.
- **Visualization**: Interactive plots with a **Time-Based Rolling Window** (e.g., show the last 10 seconds of live data).
- **Derived Signals**: Create custom calculated signals using math expressions (e.g., `sqrt(battery_voltage)`).
- **Data Logging**: Record raw network or serial streams directly to disk while visualizing.
- **Session Management**: Save and restore workspace sessions (`.sbs` files). Supports multi-tab persistence, embedded schemas, and **automatic restoration** of the last session on startup.
- **Telemetry Reliability**: Built-in detection for **packet misalignment** and framing errors in streaming modes.

## 🚀 Getting Started (Quick Start)

### 1. Load a Packet Schema
Before loading binary data or streaming, the app needs to know the structure of the packets.
*   Click **"Open Schema"** in the **SCHEMA** group.
*   Select a `.yaml` schema file.

### 2. Live Streaming (Serial or Network)
Streaming settings are **per-tab**. You can stream from multiple different sources simultaneously in different tabs.

#### Serial Port
*   **Configure**: Right-click the **Serial icon** in the **STREAM** group (or use the File menu).
    *   Select your **COM Port**, **Baud Rate**, and parameters.
    *   Set the **Rolling Window** in seconds (e.g., 10s).
*   **Start**: Click **Connect** in the dialog or left-click the **Serial icon** to toggle.

#### Network (TCP/UDP)
*   **Configure**: Right-click the **Network icon** in the **STREAM** group.
    *   **Protocol**: Choose **UDP** (listens on a local port) or **TCP** (connects to a server).
    *   **IP Address**: For TCP, the server IP. 
*   **Start**: Click **Connect** in the dialog or left-click the **Network icon** to toggle.

### 3. Static Telemetry Data
*   Click **"Open Telemetry"** in the **DATA FILE** group.
    *   **CSV**: Detects headers and loads data automatically. Settings are saved with your session.
    *   **Binary**: Uses the selected schema to decode the file.

### 4. Session Management
*   **Save**: Save your entire workspace (all tabs, signals, and schemas) to a `.sbs` file.
*   **Auto-Load**: SignalBench can automatically load your most recent session on startup, picking up exactly where you left off.

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
