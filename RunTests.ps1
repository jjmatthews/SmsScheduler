function Get-ScriptDirectory
{
    $Invocation = (Get-Variable MyInvocation -Scope 1).Value
    Split-Path $Invocation.MyCommand.Path
}

$path = Get-ScriptDirectory RunTests.ps1

$nunit = $path + "\SmsScheduler\packages\NUnit.Runners.2.6.2\tools\nunit-console.exe"

$EmailSender = Join-Path $path -childpath "\build_output\tests\EmailSenderTests.dll"
$SmsCoordinatorTests = Join-Path $path -childpath "\build_output\tests\SmsCoordinatorTests.dll"
$SmsTrackingTests = Join-Path $path -childpath "\build_output\tests\SmsTrackingTests.dll"
$SmsWebTests = Join-Path $path -childpath "\build_output\tests\SmsWebTests.dll"

& $nunit $EmailSender $SmsCoordinatorTests $SmsTrackingTests $SmsWebTests 