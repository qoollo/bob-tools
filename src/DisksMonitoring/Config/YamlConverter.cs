using DisksMonitoring.OS.DisksFinding;
using DisksMonitoring.OS.DisksFinding.Entities;
using DisksMonitoring.OS.DisksProcessing.Entities;
using System;
using System.Collections.Generic;
using System.Text;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace DisksMonitoring.Config
{
    class YamlConverter : IYamlTypeConverter
    {
        private delegate object Deserialize(string value);
        private delegate Scalar Serialize(object obj);

        private Dictionary<Type, (Serialize s, Deserialize d)> data = new Dictionary<Type, (Serialize s, Deserialize d)>
        {
            { typeof(PhysicalId), (CreateSerialize("physical_id"), PhysicalId.FromString) },
            { typeof(MountPath), (CreateSerialize("mount_path"), s => new MountPath(s)) },
            { typeof(BobPath), (CreateSerialize("bob_path"), s => new BobPath(s)) },
            { typeof(UUID), (CreateSerialize("uuid"), s => new UUID(s)) },
        };

        public bool Accepts(Type type)
        {
            return data.ContainsKey(type);
        }

        public object ReadYaml(IParser parser, Type type)
        {
            var scalar = parser.Current as Scalar;
            parser.MoveNext();
            if (data.TryGetValue(type, out var t))
                return t.d(scalar.Value);
            return null;
        }

        public void WriteYaml(IEmitter emitter, object value, Type type)
        {
            if (data.TryGetValue(type, out var t))
                emitter.Emit(t.s(value));
        }

        private static Serialize CreateSerialize(string name)
        {
            return o => new Scalar(name, o.ToString());
        }
    }
}
