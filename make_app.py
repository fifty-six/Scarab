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
exe = publish / "Scarab"

if app_dir.suffix != ".app":
    print("Error: " + str(app_dir) + " is not an .app folder.")
    exit(-1)

if not app_dir.exists():
    raise FileNotFoundError(ENOENT, strerror(ENOENT), app_dir)

if not exe.exists():
    raise FileNotFoundError(ENOENT, strerror(ENOENT), exe)

def write_executable(zfile, path, zip_path=None):
    if zip_path is None:
        zip_path = path

    with open(path, 'rb') as f:
        fbytes = f.read()

    info = ZipInfo(str(zip_path))
    info.date_time = localtime()
    # -rwx-r---r--
    info.external_attr = 0o100755 << 16
    # UNIX host
    info.create_system = 3

    zip_f.writestr(info, fbytes, ZIP_DEFLATED)
    
with ZipFile("out/mac.zip", 'w', ZIP_DEFLATED) as zip_f:
    for root, dirs, files in walk(app_dir):
        root = Path(root)

        for fname in files:
            if fname == "Scarab":
                write_executable(zip_f, root / fname)
                continue

            path = Path(root, fname)
            zip_f.write(path)

        if root.name != "Contents":
            continue

        for publish_root, _, files in walk(publish):
            publish_root = Path(publish_root)
            for fname in files:
                if fname == "Scarab":
                    continue

                overrides = { 
                        "Scarab.pdb": "run.pdb"
                }

                path = publish_root / fname
                zip_path = root / "MacOS" / overrides.get(fname, fname)

                zip_f.write(path, zip_path)

        write_executable(zip_f, publish_root / "Scarab", root / "MacOS" / "run")


print("Created mac.zip")
