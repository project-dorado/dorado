#!/usr/bin/env python3
"""
Dorado Developer MCP Server
Provides local development tools for inspecting connected Zune USB devices,
inspecting audio metadata, and auditing Zune Metro design invariants.
"""

import sys
import json
import os
import glob
import subprocess

def handle_initialize(request_id):
    return {
        "jsonrpc": "2.0",
        "id": request_id,
        "result": {
            "protocolVersion": "2024-11-05",
            "capabilities": {
                "tools": {}
            },
            "serverInfo": {
                "name": "dorado-dev-tools",
                "version": "1.0.0"
            }
        }
    }

def handle_list_tools(request_id):
    return {
        "jsonrpc": "2.0",
        "id": request_id,
        "result": {
            "tools": [
                {
                    "name": "probe_zune_devices",
                    "description": "Scans USB controllers for connected Microsoft Zune hardware (Zune 30, Zune 4/8/16, Zune 80/120, Zune HD).",
                    "inputSchema": {
                        "type": "object",
                        "properties": {},
                        "additionalProperties": False
                    }
                },
                {
                    "name": "audit_zune_design_invariants",
                    "description": "Scans project XAML, AXAML, and styling files to detect violations of the Zune Metro design language (e.g., non-zero CornerRadius, drop shadows).",
                    "inputSchema": {
                        "type": "object",
                        "properties": {
                            "directory": {
                                "type": "string",
                                "description": "Path to scan, defaults to repo root."
                            }
                        },
                        "additionalProperties": False
                    }
                }
            ]
        }
    }

def probe_zune_devices():
    zune_pids = {
        "063e": "Microsoft Zune 30",
        "0710": "Microsoft Zune 4/8/16",
        "0715": "Microsoft Zune 80/120",
        "0723": "Microsoft Zune HD"
    }
    found = []
    
    # Check Linux /sys/bus/usb/devices
    sys_usb = "/sys/bus/usb/devices"
    if os.path.exists(sys_usb):
        for dev in os.listdir(sys_usb):
            dev_path = os.path.join(sys_usb, dev)
            vendor_file = os.path.join(dev_path, "idVendor")
            product_file = os.path.join(dev_path, "idProduct")
            if os.path.isfile(vendor_file) and os.path.isfile(product_file):
                try:
                    with open(vendor_file, "r") as vf:
                        vid = vf.read().strip().lower()
                    with open(product_file, "r") as pf:
                        pid = pf.read().strip().lower()
                    if vid == "045e" and pid in zune_pids:
                        found.append({
                            "model": zune_pids[pid],
                            "vendorId": "0x045E",
                            "productId": f"0x{pid.upper()}",
                            "devicePath": dev_path
                        })
                except Exception:
                    pass
                    
    # Also attempt lsusb if available
    if not found:
        try:
            out = subprocess.check_output(["lsusb"], text=True, stderr=subprocess.DEVNULL)
            for line in out.splitlines():
                if "045e:" in line.lower():
                    for pid, name in zune_pids.items():
                        if f"045e:{pid}" in line.lower():
                            found.append({
                                "model": name,
                                "vendorId": "0x045E",
                                "productId": f"0x{pid.upper()}",
                                "raw": line
                            })
        except Exception:
            pass

    return {
        "detectedDevicesCount": len(found),
        "devices": found,
        "message": f"Found {len(found)} connected Zune hardware device(s)." if found else "No physical Zune devices detected on USB."
    }

def audit_design_invariants(target_dir=None):
    if not target_dir:
        target_dir = os.getcwd()
        
    violations = []
    axaml_files = glob.glob(os.path.join(target_dir, "**/*.axaml"), recursive=True) + \
                  glob.glob(os.path.join(target_dir, "**/*.xaml"), recursive=True)
                  
    for f in axaml_files:
        try:
            with open(f, "r", encoding="utf-8", errors="ignore") as fp:
                for line_no, line in enumerate(fp, start=1):
                    # Check for non-zero CornerRadius
                    if "CornerRadius" in line:
                        # Check if non-zero
                        valid_zeros = [
                            'CornerRadius="0"', 
                            'CornerRadius="0,0,0,0"', 
                            'CornerRadius="{DynamicResource ZeroCornerRadius}"',
                            'CornerRadius="{DynamicResource ZuneCornerRadius}"',
                            'CornerRadius="{StaticResource ZuneCornerRadius}"',
                            'Value="{StaticResource ZuneCornerRadius}"',
                            'Value="{DynamicResource ZuneCornerRadius}"',
                            '>0</CornerRadius>'
                        ]
                        if not any(v in line for v in valid_zeros):
                            violations.append({
                                "file": f,
                                "line": line_no,
                                "rule": "CornerRadius must be 0 (Zero rounded corners in Zune Metro design)",
                                "content": line.strip()
                            })
                    # Check for drop shadows
                    if "DropShadowEffect" in line or "BoxShadow" in line:
                        violations.append({
                            "file": f,
                            "line": line_no,
                            "rule": "Drop shadows are forbidden in Zune Metro design (Content Before Chrome)",
                            "content": line.strip()
                        })
        except Exception as e:
            violations.append({"file": f, "error": str(e)})
            
    return {
        "filesScanned": len(axaml_files),
        "violationsCount": len(violations),
        "violations": violations,
        "status": "PASSED" if len(violations) == 0 else "FAILED"
    }

def handle_call_tool(request_id, params):
    name = params.get("name")
    args = params.get("arguments", {})
    
    if name == "probe_zune_devices":
        res = probe_zune_devices()
        return {
            "jsonrpc": "2.0",
            "id": request_id,
            "result": {
                "content": [{"type": "text", "text": json.dumps(res, indent=2)}]
            }
        }
    elif name == "audit_zune_design_invariants":
        target = args.get("directory")
        res = audit_design_invariants(target)
        return {
            "jsonrpc": "2.0",
            "id": request_id,
            "result": {
                "content": [{"type": "text", "text": json.dumps(res, indent=2)}]
            }
        }
    else:
        return {
            "jsonrpc": "2.0",
            "id": request_id,
            "error": {"code": -32601, "message": f"Unknown tool: {name}"}
        }

def main():
    while True:
        line = sys.stdin.readline()
        if not line:
            break
        try:
            msg = json.loads(line)
            method = msg.get("method")
            req_id = msg.get("id")
            
            if method == "initialize":
                resp = handle_initialize(req_id)
            elif method == "notifications/initialized":
                continue
            elif method == "tools/list":
                resp = handle_list_tools(req_id)
            elif method == "tools/call":
                resp = handle_call_tool(req_id, msg.get("params", {}))
            else:
                resp = {"jsonrpc": "2.0", "id": req_id, "error": {"code": -32601, "message": "Method not found"}}
                
            sys.stdout.write(json.dumps(resp) + "\n")
            sys.stdout.flush()
        except Exception as e:
            err = {"jsonrpc": "2.0", "id": None, "error": {"code": -32700, "message": str(e)}}
            sys.stdout.write(json.dumps(err) + "\n")
            sys.stdout.flush()

if __name__ == "__main__":
    main()
