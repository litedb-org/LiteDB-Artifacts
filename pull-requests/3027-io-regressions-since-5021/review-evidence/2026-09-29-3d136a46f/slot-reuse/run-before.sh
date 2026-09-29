#!/bin/bash
S=$SCRATCH/slot
$S/run-tests.sh before-iosafety 'Category=IoSafety' --build
$S/run-tests.sh before-internals 'FullyQualifiedName~LiteDB.Internals.'
echo DONE > $S/before-done.txt
