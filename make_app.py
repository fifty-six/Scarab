#!/usr/bin/env python3
from os import walk, strerror
from errno import ENOENT
from zipfile import ZipInfo, ZIP_DEFLATED, ZipFile
from time import localtime
from pathlib import Path
from sys import argv

if len(argv) != 3:
    print(f"USAGE: {argv[0]} [APP_DIRECTORY] [PUBLISH DIR]")
    exit(-1)

app_dir = Path(argv[1])
publish = Path(argv[2])
exe = Path(argv[2] + "/Scarab")

if app_dir.suffix != ".app":
    print("Error: " + app_dir + " is not an .app folder.")
    exit(-1)

if not app_dir.exists():
    raise FileNotFoundError(ENOENT, strerror(ENOENT), app_dir)

if not exe.exists():
    raise FileNotFoundError(ENOENT, strerror(ENOENT), exe)

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

        for publish_root, _, files in walk(publish):
            for fname in files:
                if fname == "Scarab":
                    continue
                path = Path(publish_root, fname)
                zip_path = Path(root, mac_os, fname)
                zip_f.write(path, zip_path)

        with open(exe, 'rb') as exe_f:
            exe_bytes = exe_f.read()

        info = ZipInfo(str(Path(root, mac_os, exe.name)))
        info.date_time = localtime()
        info.external_attr = 0o100755 << 16
        # UNIX host
        info.create_system = 3

        print(oct(info.external_attr))
        
        zip_f.writestr(info, exe_bytes, ZIP_DEFLATED)
        

print("Created " + zip_name)
