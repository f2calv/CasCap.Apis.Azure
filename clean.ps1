Get-ChildItem .\ -Include bin, obj -Recurse | ForEach-Object { Remove-Item $_.fullname -Force -Recurse }
