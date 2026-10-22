"""讀取 Windows 程序峰值工作集；不支援時保留空值。"""

import ctypes
import os
from ctypes import wintypes


class ProcessEntry(ctypes.Structure):
    _fields_ = [("size", wintypes.DWORD), ("usage", wintypes.DWORD),
               ("pid", wintypes.DWORD), ("heap", ctypes.c_size_t),
               ("module", wintypes.DWORD), ("threads", wintypes.DWORD),
               ("parent", wintypes.DWORD), ("priority", wintypes.LONG),
               ("flags", wintypes.DWORD), ("name", wintypes.WCHAR * 260)]


def unity_child(pid):
    if os.name != "nt": return None
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel.CreateToolhelp32Snapshot.argtypes = [wintypes.DWORD, wintypes.DWORD]
    kernel.CreateToolhelp32Snapshot.restype = wintypes.HANDLE
    kernel.Process32FirstW.argtypes = [wintypes.HANDLE, ctypes.POINTER(ProcessEntry)]
    kernel.Process32NextW.argtypes = [wintypes.HANDLE, ctypes.POINTER(ProcessEntry)]
    kernel.CloseHandle.argtypes = [wintypes.HANDLE]
    snapshot = kernel.CreateToolhelp32Snapshot(2, 0)
    if snapshot == ctypes.c_void_p(-1).value: return None
    try:
        entry = ProcessEntry()
        entry.size = ctypes.sizeof(entry)
        processes = {}
        valid = kernel.Process32FirstW(snapshot, ctypes.byref(entry))
        while valid:
            processes[entry.pid] = (entry.parent, entry.name)
            valid = kernel.Process32NextW(snapshot, ctypes.byref(entry))
        descendants = {pid}
        while True:
            added = {p for p, (parent, _) in processes.items() if parent in descendants} - descendants
            if not added: break
            descendants.update(added)
        return next((p for p in descendants if p in processes and processes[p][1].lower() == "world.exe"), None)
    finally:
        kernel.CloseHandle(snapshot)


class MemoryCounters(ctypes.Structure):
    _fields_ = [("size", ctypes.c_uint32), ("pageFaultCount", ctypes.c_uint32),
               ("peakWorkingSet", ctypes.c_size_t), ("workingSet", ctypes.c_size_t),
               ("quotaPeakPagedPool", ctypes.c_size_t), ("quotaPagedPool", ctypes.c_size_t),
               ("quotaPeakNonPagedPool", ctypes.c_size_t), ("quotaNonPagedPool", ctypes.c_size_t),
               ("pagefile", ctypes.c_size_t), ("peakPagefile", ctypes.c_size_t)]


def peak_memory(pid):
    if os.name != "nt":
        return None
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    psapi = ctypes.WinDLL("psapi", use_last_error=True)
    kernel.OpenProcess.argtypes = [ctypes.c_uint32, ctypes.c_int, ctypes.c_uint32]
    kernel.OpenProcess.restype = ctypes.c_void_p
    kernel.CloseHandle.argtypes = [ctypes.c_void_p]
    psapi.GetProcessMemoryInfo.argtypes = [ctypes.c_void_p, ctypes.POINTER(MemoryCounters), ctypes.c_uint32]
    handle = kernel.OpenProcess(0x1000 | 0x10, False, pid)
    if not handle:
        return None
    try:
        counters = MemoryCounters()
        counters.size = ctypes.sizeof(counters)
        if not psapi.GetProcessMemoryInfo(handle, ctypes.byref(counters), counters.size):
            return None
        return counters.peakWorkingSet
    finally:
        kernel.CloseHandle(handle)
