for f in $(find $1 -type f -print) ; do
    hash=$(xxhsum -H2 < $f | awk '{ print $1 }')
    size=$(stat -c%s $f)
    echo f$f l$size c$hash
done