#!/usr/bin/env python3
from os import walk, strerror
from errno import ENOENT
from zipfile import ZipInfo, ZIP_DEFLATED, ZipFile
from time import localtime
from pathlib import Path
from sys import argv

if len(argv) != 3:
    print(f"USAGE: {argv[0]} [APP_DIRECTORY] [EXECUTABLE]")
    exit(-1)

app_dir = Path(argv[1])
exe = Path(argv[2])

if app_dir.suffix != ".app":
    print("Error: " + app_dir + " is not an .app folder.")
    exit(-1)

if not app_dir.exists():
    raise FileNotFoundError(errorno.ENOENT, strerror(ENOENT), app_dir)

if not exe.exists():
    raise FileNotFoundError(errorno.ENOENT, strerror(ENOENT), app_dir)

zip_name = app_dir.stem + ".zip"

with ZipFile(zip_name, 'w', ZIP_DEFLATED) as zip_f:
    found_bin = False
    
    for root, dirs, files in walk(app_dir):

        for fname in files:
            path = Path(root, fname)
            zip_f.write(path)

        mac_os = next((x for x in dirs if x == "MacOS"), None)
        
        if not mac_os:
            continue

        with open(exe, 'rb') as exe_f:
            exe_bytes = exe_f.read()

        info = ZipInfo(str(Path(root, mac_os, exe.name[:-1])))
        info.date_time = localtime()
        info.external_attr = 0o100755 << 16
        # UNIX host
        info.create_system = 3

        print(oct(info.external_attr))
        print(info)
        
        zip_f.writestr(info, exe_bytes, ZIP_DEFLATED)
        

print("Created " + zip_name)
