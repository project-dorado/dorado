---
name: zune-hardware-sync
description: >-
  Use this skill when developing, debugging, testing, or maintaining the physical Zune device
  synchronization subsystem, including USB MTP/MTPZ protocols, ZMDB database parsing,
  USB-PPP network interception for artist metadata, and wireless sync.
---

# Zune Hardware Synchronization Architecture Guide

This guide details the technical protocols, binary formats, and hardware communication procedures
required to interface with physical Microsoft Zune media players (Zune 30, Zune 4/8/16, Zune 80/120, and Zune HD).

---

## 1. Hardware Identifiers & USB Enumeration

All physical Zune devices share Microsoft's USB Vendor ID (`0x045E`):

| Device Model | USB Product ID | Storage | Display / Notes |
| :--- | :--- | :--- | :--- |
| **Zune 30** | `0x063E` | 30 GB HDD | 240x320 LCD, mechanical d-pad |
| **Zune 4 / 8 / 16** | `0x0710` | 4, 8, 16 GB Flash | 240x320 LCD, Zune Pad |
| **Zune 80 / 120** | `0x0715` | 80, 120 GB HDD | 240x320 LCD, Zune Pad |
| **Zune HD (16/32/64)**| `0x0723` | 16, 32, 64 GB Flash| 480x272 OLED, Capacitive Multi-touch, Tegra APX |

### OS USB Backends
- **Linux (`libusb-1.0`):**
  Requires udev rules in `/etc/udev/rules.d/51-zune.rules`:
  ```udev
  SUBSYSTEM=="usb", ATTR{idVendor}=="045e", ATTR{idProduct}=="063e", MODE="0666"
  SUBSYSTEM=="usb", ATTR{idVendor}=="045e", ATTR{idProduct}=="0710", MODE="0666"
  SUBSYSTEM=="usb", ATTR{idVendor}=="045e", ATTR{idProduct}=="0715", MODE="0666"
  SUBSYSTEM=="usb", ATTR{idVendor}=="045e", ATTR{idProduct}=="0723", MODE="0666"
  ```
- **Windows (`WinUSB`):**
  Bound using the WinUSB driver so userspace software has direct access to bulk and interrupt pipes.

---

## 2. MTP & MTPZ Protocol Handshake

Zunes communicate using the Media Transfer Protocol (MTP) with proprietary Microsoft extensions (MTPZ).

1. **Standard MTP Session:**
   - Open Session (`OpenSession(1)`).
   - Query Device Capabilities and Storage IDs (`GetStorageIDs`).
2. **MTPZ Authentication Challenge:**
   - The device requires an MTPZ handshake to unlock full read/write capabilities and prevent firmware lockup.
   - Requires `.mtpz-data` certificate and key validation.
   - Handshake sequence exchanges encrypted tokens over vendor-specific MTP commands.

---

## 3. Fast ZMDB (Zune Media Database) Parser

Iterating objects one-by-one via standard MTP commands (`GetObjectHandles` + `GetObjectInfo`) takes hours for large collections (10,000+ tracks). 

Instead, Dorado utilizes **fast ZMDB binary parsing**:
1. Retrieve the device's internal binary database file (`zune.zmdb` located in the root media directory) via a single MTP streaming download.
2. Parse the binary table structure using the **F-marker record extractor algorithm**:
   - The database stores fixed-length record headers preceded by signature sync markers (F-markers).
   - Fast sequential memory scan extracts Track ID, Title, Artist string, Album string, Duration, Bitrate, Rating (Heart state), and File path.
3. The entire device library of 30,000 tracks is indexed in under 2 seconds.

---

## 4. USB PPP / HTTP Reverse Interceptor

The Zune HD (and Zune 30/80/120 firmware 3.0+) automatically fetches artist backgrounds and biography XML over USB when connected to a host:

### How it Works:
1. **USB Transport Layer:** The host and Zune exchange raw network frames using MTP vendor commands:
   - `0x922C`: Send packet to Zune.
   - `0x922D`: Poll/receive packet from Zune.
2. **PPP / IPCP Handshake:**
   - The host implements a lightweight Point-to-Point Protocol (PPP) and Link Control Protocol (LCP).
   - Negotiates IP addresses via IPCP:
     - **Host IP:** `192.168.55.100`
     - **Zune Device IP:** `192.168.55.101`
3. **Embedded DNS Server:**
   - Listens on UDP port 53 on `192.168.55.100`.
   - Any query for `catalog.zune.net`, `image.catalog.zune.net`, or `resources.zune.net` resolves to `192.168.55.100`.
4. **Embedded HTTP Server:**
   - Listens on TCP port 80.
   - Serves Zune XML catalog format:
     - `/v3.0/en-US/music/artist/{mbid}`
     - `/v3.0/en-US/music/artist/{mbid}/biography`
     - `/v3.0/en-US/music/artist/{mbid}/deviceBackgroundImage`
   - The Zune immediately renders the high-res artist wallpaper and bio on the device screen!

---

## 5. Wireless Sync (SSDP & PTP/IP)

- **SSDP Discovery:** The Zune broadcasts SSDP `M-SEARCH` packets on UDP port 1900 with device type `urn:schemas-microsoft-com:device:zune-sync:1`.
- **PTP/IP Connection:** Host connects back to the Zune's wireless IP address on TCP port 15740 to perform sync over Wi-Fi without needing a USB cable.
