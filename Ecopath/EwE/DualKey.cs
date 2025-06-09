namespace Ecopath.EwE
{
    internal record DualKey(string c1, string c2)
    {
        public static DualKey Make(string c1, string d2)
        {
            if (string.IsNullOrEmpty(c1)) c1 = "(any)";
            if (string.IsNullOrEmpty(d2)) d2 = "(any)";
            return new DualKey(c1.ToUpper(), d2.ToUpper());
        }
    }
}
