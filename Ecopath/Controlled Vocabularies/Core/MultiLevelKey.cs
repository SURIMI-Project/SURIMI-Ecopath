using ControlledVocabularies.Descriptors;
using ControlledVocabularies.Utils;
using Ecopath.Services;
using System.Data;
using System.Reflection;
using System.Text;

namespace ControlledVocabularies.Core
{
    /// <summary>
    /// Represents a multi-level, self-describing key (e.g., for species or fleets)
    /// </summary>
    /// <todo>Implement IFreezable (IsFrozen/Freeze) and guard all mutators (SetField/Parse/SetFieldDescriptor).</todo>
    /// <todo>Add Clone(bool frozen=false) for safe copies when mutation is needed by callers.</todo>
    /// <todo>Ensure SetField() normalizes field names with FieldPolicy.ForSchema; values via FieldPolicy.ForValue(kind).</todo>
    /// <todo>Consider exposing an IReadOnlyDictionary<string, MultiLevelKeyField> view for fields.</todo>
    /// <todo>Define value equality & stable hash if keys are used as dictionary keys (document semantics).</todo>

    public class MultiLevelKey
    {
        #region Private parts 

        private Dictionary<string, MultiLevelKeyField> m_fields { get; set; } = new();
        private Dictionary<string, KeyFieldDescriptor> m_descriptors { get; set; } = new();

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
        public static MultiLevelKey FromObject(object source, KeyDomain domainHint, IKeyFieldDescriptorRegistry? registry = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var pairs = source.GetType()
                              .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                              .Where(p => p.CanRead && p.PropertyType == typeof(string))
                              .Select(p => (FieldPolicy.ForSchema(p.Name), (string?)p.GetValue(source)));


            return FromPairs(pairs, domainHint, registry);
        }

        /// <summary>
        /// Factory method
        /// </summary>
        /// <param name="row"></param>
        /// <param name="domainHint"></param>
        /// <param name="registry"></param>
        /// <returns></returns>
        public static MultiLevelKey FromDataRow(DataRow row, KeyDomain domainHint, IKeyFieldDescriptorRegistry? registry = null)
        {
            var pairs = row.Table.Columns.Cast<DataColumn>()
                           .Select(c => (FieldPolicy.ForSchema(c.ColumnName), row[c] as string));

            return FromPairs(pairs, domainHint, registry);
        }

        /// <summary>
        /// Factory method
        /// </summary>
        /// <param name="keyStr"></param>
        /// <param name="domainHint"></param>
        /// <param name="registry"></param>
        /// <returns></returns>
        public static MultiLevelKey FromString(string keyStr, KeyDomain domainHint, IKeyFieldDescriptorRegistry? registry = null)
        {
            var pairs = keyStr.Split(';', StringSplitOptions.RemoveEmptyEntries)
                              .Select(kvp => kvp.Split('='))
                              .Where(parts => parts.Length == 2)
                              .Select(parts => (FieldPolicy.ForSchema(parts[0]), parts[1]));

            return FromPairs(pairs!, domainHint, registry);
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
        public static MultiLevelKey FromPairs(IEnumerable<(string field, string? value)> pairs,  KeyDomain domainHint, IKeyFieldDescriptorRegistry? registry = null, bool strict = true)
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
                            MultiLevelKeyField? val = null;
                            if (m_fields.TryGetValue(FieldPolicy.ForSchema(prop.Name), out val))
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

            key = FieldPolicy.ForRecordKey(key);

            if (string.IsNullOrWhiteSpace(value))
            {
                this.m_fields.Remove(key);
                return;
            }

            int iSep = value.IndexOf(':');
            string vocab = (iSep == -1 || bPurgeVocabularyName) ? string.Empty : value.Substring(0, iSep);
            value = (iSep == -1) ? value : value.Substring(iSep + 1);

            this.m_fields[key] = new MultiLevelKeyField(value, vocab);

            // Try to complement a missing KeyFieldDescriptor if allowed
            if (!this.m_descriptors.ContainsKey(key) && !_strict)
            {
                KeyFieldDescriptorRegistry? registry = GlobalServiceLocator.Get<KeyFieldDescriptorRegistry>();
                if (registry != null)
                {
                    KeyFieldDescriptor? descr = registry!.Get(Domain, key) ?? null;
                    if (descr != null)
                    {
                        this.m_descriptors[key] = descr;
                    }
                }
            }
        }

         public MultiLevelKeyField? GetField(string key)
        {
            key = FieldPolicy.ForRecordKey(key);
            if (this.m_fields.TryGetValue(key, out var value)) return value;
            return null;
        }

        public void SetFieldDescriptor(string key, KeyFieldDescriptor? descriptor)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            key = FieldPolicy.ForRecordKey(key);
            if (descriptor == null)
            {
                this.m_descriptors.Remove(key);
                return;
            }
            m_descriptors[key] = descriptor;
        }

        public IEnumerable<string> FieldNames => this.m_fields.Keys;

        public KeyDomain FieldDomain(string field)
        {
            KeyFieldDescriptor? d = this.GetFieldDescriptor(field);
            return d?.Domain ?? this.Domain;
        }

        public KeyPurpose FieldPurpose(string field)
        {
            KeyFieldDescriptor? d = this.GetFieldDescriptor(field);
            return d?.Purpose ?? KeyPurpose.NotSet;
        }

        /// <summary>
        /// Returns a canonical string representation of the key
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            StringBuilder sb = new();

            sb.Append(string.Join(";", this.m_fields.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}")));
            sb.Append($";domain={this.Domain.ToString()}");

            if (this.TimeStamp > DateTime.MinValue)
                sb.Append($"timestamp={this.TimeStamp.ToShortDateString()}");

            return sb.ToString().ToLowerInvariant();
        }

        public static MultiLevelKey Empty => new MultiLevelKey(KeyDomain.NotSet);

        #region Internals

        private KeyFieldDescriptor? GetFieldDescriptor(string key)
        {
            key = FieldPolicy.ForRecordKey(key);
            if (this.m_descriptors.TryGetValue(key, out var value)) return value;
            return null;
        }

        #endregion // Internals
    }
}