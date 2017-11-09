#!/bin/bash

./build.sh

ZIPFILE="$PWD/out/msbuild-project-tools.zip"

if [ -f $ZIPFILE ]; then
    rm $ZIPFILE
fi

echo "Creating $ZIPFILE..."

zip -qr $ZIPFILE out/

echo "Done."
