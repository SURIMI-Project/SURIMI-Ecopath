namespace Ecopath.Services
{
    public static class GlobalServiceLocator 
    { 
        private static List<object> SafeServices = new(); 
        
        public static void Register(object service) 
        { 
            SafeServices.Add(service); 
        } 

        public static T? Get<T>() where T : class 
        { 
            return SafeServices.OfType<T>().FirstOrDefault() ?? null; 
        } 
        
        public static bool TryGet<T>(out T? service) where T : class 
        { 
            service = Get<T>(); return service != null; 
        }

        public static void Replace<T>(T instance) where T : class
        {
            SafeServices.RemoveAll(s => s is T);
            SafeServices.Add(instance);
        }

        public static void Reset() => SafeServices.Clear();
    }

}
