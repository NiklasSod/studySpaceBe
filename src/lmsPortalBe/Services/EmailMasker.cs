namespace lmsPortalBe.Services;

public static class EmailMasker
{
  public static string Mask(string email)
  {
    var atIndex = email.LastIndexOf('@');
    if (atIndex <= 0 || atIndex == email.Length - 1)
    {
      return "*****";
    }

    return $"{email[0]}*****{email[atIndex..]}";
  }
}
