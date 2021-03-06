/*
 * The MIT License (MIT)
 *
 * Copyright (c) 2015 Microsoft Corporation
 * 
 * -=- Robust Distributed System Nucleus (rDSN) -=- 
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

/*
 * Description:
 *     What is this file about?
 *
 * Revision history:
 *     Feb., 2016, @imzhenyu (Zhenyu Guo), done in Tron project and copied here
 *     xxxx-xx-xx, author, fix bug about xxx
 */

using System;
using System.Collections.Generic;
using System.IO;

namespace rDSN.Tron.Contract
{
    public enum ConsistencyLevel
    {
        Any,
        Eventual,
        Causal,
        Strong  // Primary-backup or Quorum
    }

    public enum PartitionType
    { 
        None,
        Fixed,
        Dynamic
    }

    public class ServiceProperty
    {
        /// <summary>
        /// whether the service needs to be deployed by our infrastructure, or it is an existing service that we can invoke directly
        /// </summary>
        public bool? IsDeployedAlready { get; set; }

        /// <summary>
        /// whether the service is a primtive service or a service composed in TRON
        /// </summary>
        public bool? IsPrimitive { get; set; }

        /// <summary>
        /// whether the service is partitioned and will be deployed on multiple machines
        /// </summary>
        public bool? IsPartitioned { get; set; }

        /// <summary>
        /// whether the service is stateful or stateless. A stateless service can lose its state safely.
        /// </summary>
        public bool? IsStateful { get; set; }

        /// <summary>
        /// whether the service is replicated (multiple copies)
        /// </summary>
        public bool? IsReplicated { get; set; }
    }

    /// <summary>
    /// service description
    /// </summary>
    public class Service
    {
        public Service(string package, string url, string name = "")
        {
            PackageName = package;
            Url = url;
            Name = !string.IsNullOrEmpty(name) ? name : url;

            Properties = new ServiceProperty();
            Spec = new ServiceSpec();
        }
        
        /// <summary>
        /// package name for this service (the package is published and stored in a service store)
        /// with the package, the service can be deployed with a deployment service
        /// </summary>
        public string PackageName { get; private set; }
        
        /// <summary>
        /// universal remote link for the service, which is used for service address resolve
        /// example:  http://service-manager-url/service-name
        /// </summary>
        public string Url { get; private set; }

        /// <summary>
        /// service name for print and RPC resolution (e.g., service-name.method-name for invoking a RPC)
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// service properties
        /// </summary>
        public ServiceProperty Properties { get; set; }
        
        /// <summary>
        /// spec info
        /// </summary>
        public ServiceSpec Spec { get; private set; }

        public string TypeName ()
        {
            return (GetType().Namespace + "." + GetType().Name.Substring("Service_".Length));
        }

        public string PlainTypeName ()
        {
            return TypeName().Replace('.', '_');
        }


        public ServiceSpec ExtractSpec()
        {
            if (Spec.Directory != "") return Spec;
            Spec.Directory = ".";

            var files = new List<string> {Spec.MainSpecFile};
            files.AddRange(Spec.ReferencedSpecFiles);

            foreach (var f in files)
            {
                if (File.Exists(Path.Combine(Spec.Directory, f))) continue;
                var stream = GetType().Assembly.GetManifestResourceStream(f);
                using (Stream file = File.Create(Path.Combine(Spec.Directory, f)))
                {
                    int len;
                    var buffer = new byte[8 * 1024];                            
                    while ((len = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        file.Write(buffer, 0, len);
                    }
                }
            }
            return Spec;
        }
    }

    public class PrimitiveService
    {
        public PrimitiveService(string name, string classFullName, string classShortName)
        {
            Name = name;
            ServiceClassFullName = classFullName;
            ServiceClassShortName = classShortName;

            ReadConsistency = ConsistencyLevel.Any;
            WriteConsistency = ConsistencyLevel.Any;

            PartitionKey = null;
            PartitionType = PartitionType.None;
            PartitionCount = 1;
        }

        protected PrimitiveService Replicate(
            int minDegree, 
            int maxDegree, 
            ConsistencyLevel readConsistency = ConsistencyLevel.Any, 
            ConsistencyLevel writeConsistency = ConsistencyLevel.Any
            )
        {
            ReplicateMinDegree = minDegree;
            ReplicateMaxDegree = maxDegree;
            ReadConsistency = readConsistency;
            WriteConsistency = writeConsistency;

            return this;
        }

        protected PrimitiveService Partition(Type key, PartitionType type, int partitionCount = 1)
        {
            PartitionKey = key;
            PartitionType = type;
            PartitionCount = partitionCount;
            
            return this;
        }

        protected PrimitiveService SetDataSource(string dataSource) // e.g., cosmos structured stream
        {
            DataSource = dataSource;
            return this;
        }

        protected PrimitiveService SetConfiguration(string uri)
        {
            Configuration = uri;
            return this;
        }

        public Type PartitionKey { get; private set; }
        public PartitionType PartitionType { get; private set; }
        public int PartitionCount { get; private set; }

        public int ReplicateMinDegree { get; private set; }
        public int ReplicateMaxDegree { get; private set; }
        public ConsistencyLevel ReadConsistency { get; private set; }
        public ConsistencyLevel WriteConsistency { get; private set; }

        public string DataSource { get; private set; }
        public string Configuration { get; private set; }
        public string Name { get; private set; }
        public string ServiceClassFullName { get; private set; }
        public string ServiceClassShortName { get; private set; }
    }

    public class PrimitiveService<TSelf> : PrimitiveService
        where TSelf : PrimitiveService<TSelf>
    {
        public PrimitiveService(string name, string classFullName)
            : base(name, classFullName, classFullName.Substring(classFullName.LastIndexOfAny(new[]{':', '.'}) + 1))
        {
        }

        public new TSelf Replicate(
            int minDegree,
            int maxDegree,
            ConsistencyLevel readConsistency = ConsistencyLevel.Any,
            ConsistencyLevel writeConsistency = ConsistencyLevel.Any
            )
        {
            return base.Replicate(minDegree, maxDegree, readConsistency, writeConsistency) as TSelf;
        }

        public new TSelf Partition(Type key, PartitionType type = PartitionType.Dynamic, int partitionCount = 1)
        {
            return base.Partition(key, type, partitionCount) as TSelf;
        }

        public TSelf DataSource(string dataSource) // e.g., cosmos structured stream
        {
            return SetDataSource(dataSource) as TSelf;
        }

        public TSelf Configuration(string uri)
        {
            return SetConfiguration(uri) as TSelf;
        }
    }

    public class Sla
    {
        public enum Metric
        { 
            Latency99Percentile,
            Latency95Percentile,
            Latency90Percentile,
            Latency50Percentile,

            WorkflowConsistency
        }

        public enum WorkflowConsistencyLevel
        { 
            Any,
            Atomic,
            Acid
        }

        public Sla Add<TValue>(Metric prop, TValue value)
        {
            return Add(prop, value.ToString());
        }

        public Sla Add(Metric prop, string value)
        {
            _mProperties.Add(prop, value);
            return this;
        }

        public string Get(Metric prop)
        {
            string v;
            _mProperties.TryGetValue(prop, out v);
            return v;
        }

        private Dictionary<Metric, string> _mProperties = new Dictionary<Metric, string>();
    }
}
