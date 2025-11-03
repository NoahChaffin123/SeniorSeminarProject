namespace AssassinsProject.Services.Email
{
    /// <summary>
    /// Binds to the "Email" configuration section.
    /// Required keys:
    ///   Email:ConnectionString
    ///   Email:FromAddress
    /// </summary>
    public class EmailOptions
    {
        public string? ConnectionString { get; set; }
        public string? FromAddress { get; set; }
    }
}
