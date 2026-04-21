namespace ReportPackaging.Data.Models
{
    public class AzureUser
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public string NameKo { get; set; } = string.Empty;
        public int CenterId { get; set; }
        public string CenterName { get; set; } = string.Empty;
        public int DeptId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public int TeamId { get; set; }
        public string Country { get; set; } = string.Empty;
        public string BankAccountQTZ { get; set; } = string.Empty;
        public string BankAccountUSD { get; set; } = string.Empty;
        public string CheckName { get; set; } = string.Empty;

        public string DisplayName => !string.IsNullOrEmpty(NameEn) ? NameEn
            : (!string.IsNullOrEmpty(NameKo) ? NameKo : Email);
    }
}
