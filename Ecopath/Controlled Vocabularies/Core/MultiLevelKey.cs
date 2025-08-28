using ControlledVocabularies.Descriptors;
using ControlledVocabularies.Utils;
using Ecopath.Services;
using System.Collections.Immutable;
using System.Data;
using System.Numerics;
using System.Reflection;
using System.Text;

namespace ControlledVocabularies.Core
{
    /// <inheritdoc/>
    /// <todo>Implement IFreezable (IsFrozen/Freeze) and guard all mutators (SetField/Parse/SetFieldDescriptor).</todo>
    /// <todo>Add Clone(bool frozen=false) for safe copies when mutation is needed by callers.</todo>
    /// <todo>Ensure SetField() normalizes field names with FieldPolicy.ForSchema; values via FieldPolicy.ForValue(kind).</todo>
    /// <todo>Consider exposing an IReadOnlyDictionary<string, MultiLevelKeyField> view for fields.</todo>
    /// <todo>Define value equality & stable hash if keys are used as dictionary keys (document semantics).</todo>

    public class MultiLevelKey : IMultiLevelKey
    {
        #region Private parts 

        private Dictionary<string, IKeyFieldDescriptor> Descriptors { get; set; } = new();
        private Dictionary<string, IMultiLevelKeyField> Fields { get; set; } = new();

        private bool _strict = false;

        #endregion // Private parts (tee hee hee)

        #region Constructor

        /// <summary>
        /// Hidden constructor; this class can only be generated via one of the factory methods.
        /// </summary>
        protected MultiLevelKey(KeyDomain domain)
        {
            Domain = domain;
        }

        #endregion // Constructor

        #region Factory methods 

        /// <summary>
        /// Factory method
        /// </summary>
        /// <param name="source"></param>
        /// <param name="domainHint"></param>
        /// <param name="registry"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static MultiLevelKey FromObject(object source, KeyDomain domainHint, IKeyFieldDescriptorRegistry? registry = null, bool strict = false)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var pairs = source.GetType()
                              .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                              .Where(p => p.CanRead && p.PropertyType == typeof(string))
                              .Select(p => (FieldPolicy.ForSchema(p.Name), (string?)p.GetValue(source)));


            return FromPairs(pairs, domainHint, registry, strict: strict);
        }

        /// <summary>
        /// Factory method
        /// </summary>
        /// <param name="row"></param>
        /// <param name="domainHint"></param>
        /// <param name="registry"></param>
        /// <returns></returns>
        public static MultiLevelKey FromDataRow(DataRow row, KeyDomain domainHint, IKeyFieldDescriptorRegistry? registry = null, bool strict = false)
        {
            var dt = row.Table;
            var pairs = new List<(string, string?)>(dt.Columns.Count);

            foreach (DataColumn col in dt.Columns)
            {
                if (row.IsNull(col)) continue; // or assign string.Empty if you prefer
                var value = row[col] as string ?? row[col]?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(value)) continue;

                pairs.Add((FieldPolicy.ForSchema(col.ColumnName), value));
            }
            return FromPairs(pairs, domainHint, registry, strict: strict);
        }

        /// <summary>
        /// Factory method
        /// </summary>
        /// <param name="keyStr"></param>
        /// <param name="domainHint"></param>
        /// <param name="registry"></param>
        /// <returns></returns>
        public static MultiLevelKey FromString(string keyStr, KeyDomain domainHint, IKeyFieldDescriptorRegistry? registry = null, bool strict = false)
        {
            var pairs = keyStr.Split(';', StringSplitOptions.RemoveEmptyEntries)
                              .Select(kvp => kvp.Split('='))
                              .Where(parts => parts.Length == 2)
                              .Select(parts => (FieldPolicy.ForSchema(parts[0]), parts[1]));

            return FromPairs(pairs!, domainHint, registry, strict: strict);
        }

        /// <summary>
        /// Factory method
        /// </summary>
        /// <param name="pairs"></param>
        /// <param name="domainHint"></param>
        /// <param name="registry"></param>
        /// <param name="strict">Flag to enforce the use of registered variables only.</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static MultiLevelKey FromPairs(IEnumerable<(string field, string? value)> pairs, KeyDomain domainHint, IKeyFieldDescriptorRegistry? registry = null, bool strict = false)
        {
            var mlk = new MultiLevelKey(domainHint) { _strict = strict };

            if (registry == null)
                registry = GlobalServiceLocator.Get<KeyFieldDescriptorRegistry>();

            foreach (var (field, value) in pairs)
            {
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                KeyFieldDescriptor? descriptor = registry?.Get(domainHint, field) ?? null;

                // If strict, descriptors are mandatory. Field values will NOT be set for missing descriptors.
                if (descriptor == null && strict)
                    continue;

                mlk.SetField(field, value);
                mlk.SetFieldDescriptor(field, descriptor);
            }

            return mlk;
        }

        #endregion // Factory methods

        public T? ToObject<T>(bool includeVocabulary = true)
        {
            var fields = new Dictionary<string, string>();

            var obj = Activator.CreateInstance(typeof(T));
            if (obj != null)
            {
                var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var prop in props)
                {
                    if (prop != null)
                    {
                        if (prop.CanWrite && prop.PropertyType == typeof(string))
                        {
                            IMultiLevelKeyField? val = null;
                            if (Fields.TryGetValue(FieldPolicy.ForSchema(prop.Name), out val))
                                prop.SetValue(obj, val.ToString(includeVocabulary));
                            else
                                prop.SetValue(obj, string.Empty);
                        }
                    }
                }
            }
            return (T?)obj;
        }

        public KeyDomain Domain { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        /// <todo>
        /// Make useful for vocabulary versioning
        /// </todo>
        public DateTime TimeStamp { get; set; } = DateTime.MinValue;


        public bool Parse(string keyStr)
        {
            if (string.IsNullOrWhiteSpace(keyStr)) return false;

            foreach (var kvpair in keyStr.Split(';'))
            {
                var parts = kvpair.Split('=');
                if (parts.Length == 2)
                {
                    this.SetField(parts[0], parts[1]);
                }
            }
            return true;
        }

        public void SetField(string key, string value, bool bPurgeVocabularyName = false)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            key = FieldPolicy.ForSchema(key);

            if (string.IsNullOrWhiteSpace(value))
            {
                this.Fields.Remove(key);
                return;
            }

            int iSep = value.IndexOf(':');
            string vocab = (iSep == -1 || bPurgeVocabularyName) ? string.Empty : value.Substring(0, iSep);
            value = (iSep == -1) ? value : value.Substring(iSep + 1);

            this.Fields[key] = new MultiLevelKeyField(value, vocab);

            // Try to complement a missing KeyFieldDescriptor if allowed
            if (!this.Descriptors.ContainsKey(key) && !_strict)
            {
                KeyFieldDescriptorRegistry? registry = GlobalServiceLocator.Get<KeyFieldDescriptorRegistry>();
                if (registry != null)
                {
                    KeyFieldDescriptor? descr = registry!.Get(Domain, key) ?? null;
                    if (descr != null)
                    {
                        this.Descriptors[key] = descr;
                    }
                }
            }
        }

        public IMultiLevelKeyField? GetField(string key)
        {
            key = FieldPolicy.ForSchema(key);
            if (this.Fields.TryGetValue(key, out var value)) return value;
            return null;
        }

        public void SetFieldDescriptor(string key, IKeyFieldDescriptor? descriptor)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            key = FieldPolicy.ForSchema(key);
            if (descriptor == null)
            {
                this.Descriptors.Remove(key);
                return;
            }
            Descriptors[key] = descriptor;
        }

        public IEnumerable<string> FieldNames => this.Fields.Keys;

        public KeyDomain FieldDomain(string field)
        {
            IKeyFieldDescriptor? d = this.GetFieldDescriptor(field);
            return d?.Domain ?? this.Domain;
        }

        public KeyPurpose FieldPurpose(string field)
        {
            IKeyFieldDescriptor? d = this.GetFieldDescriptor(field);
            return d?.Purpose ?? KeyPurpose.NotSet;
        }

        /// <summary>
        /// Returns a canonical string representation of the key
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {

            var keys = Fields.Keys.ToArray();
            Array.Sort(keys);

            StringBuilder sb = new();
            for (int i=0; i< keys.Length; i++)
            {
                if (i > 0) sb.Append(";");
                sb.Append(keys[i]); sb.Append('='); sb.Append(Fields[keys[i]]);
            }
            sb.Append($";domain={this.Domain.ToString()}");

            if (this.TimeStamp > DateTime.MinValue)
                sb.Append($"timestamp={this.TimeStamp.ToShortDateString()}");

            return sb.ToString().ToLowerInvariant();
        }

        public static MultiLevelKey Empty => new MultiLevelKey(KeyDomain.NotSet);

        #region Internals

        private IKeyFieldDescriptor? GetFieldDescriptor(string key)
        {
            key = FieldPolicy.ForSchema(key);
            if (this.Descriptors.TryGetValue(key, out var value)) return value;
            return null;
        }

        #endregion // Internals
    }
}