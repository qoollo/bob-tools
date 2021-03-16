using System;
using System.Collections.Generic;
using System.Text;

namespace DisksMonitoring.OS.DisksFinding.Entities
{
    struct Filesystem
    {
        private readonly string data;

        public Filesystem(string data)
        {
            this.data = data;
        }

        public override string ToString()
        {
            return data;
        }

        public static explicit operator string(Filesystem fs) => fs.data;
    }
}
