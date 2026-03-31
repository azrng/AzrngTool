dotnet publish -r win-x64 -c Release
Remove-Item AzrngTools\bin\Release\net8.0\win-x64\publish\AzrngTools.pdb
# makensis installer.nsi