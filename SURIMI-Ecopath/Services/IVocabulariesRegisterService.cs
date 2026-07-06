namespace Ecopath.Services
{
    /// <summary>
    /// This service is responsible for registering the different vocabularies that the application may be interested in. It also registers the different species, gear, and market fields that the application may be interested in.
    /// </summary>
    public interface IVocabulariesRegisterService
    {
        /// <summary>
        /// This method loads and registers the different vocabularies that the application may be interested in. It also registers the different species, gear, and market fields that the application may be interested in.
        /// </summary>
        void RegisterVocabularies();
    }
}