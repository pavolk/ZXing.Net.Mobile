var target = Argument("target", "libs");
var version = Argument("nugetversion", "");

var libs = new Dictionary<string, string> {
	{ "./ZXing.Net.Mobile.sln", "Any" },
	{ "./ZXing.Net.Mobile.Forms.sln", "Any" }
};

var samples = new Dictionary<string, string> {
	{ "./Samples/Android/Sample.Android.sln", "Any" },
	{ "./Samples/iOS/Sample.iOS.sln", "Any" },
	{ "./Samples/WindowsPhone/Sample.WindowsPhone.sln", "Win" },
	{ "./Samples/WindowsUniversal/Sample.WindowsUniversal.sln", "Win" },
	{ "./Samples/Forms/Sample.Forms.sln", "Win" },
};

var buildAction = new Action<Dictionary<string, string>> (solutions => {

	foreach (var sln in solutions) {

		if ((sln.Value == "Any")
				|| (sln.Value == "Win" && IsRunningOnWindows ())
				|| (sln.Value == "Mac" && IsRunningOnUnix ())) {

			NuGetRestore (sln.Key);
			
			if (IsRunningOnWindows ())
				MSBuild (sln.Key, c => { 
					c.Configuration = "Release";
					c.MSBuildPlatform = MSBuildPlatform.x86;
				});
			else 
				DotNetBuild (sln.Key, c => c.Configuration = "Release");
		}
	}
});

Task ("libs").Does (() => 
{
	buildAction (libs);
});


Task ("samples").Does (() => 
{
	buildAction (samples);
});


Task ("tools").WithCriteria (!FileExists ("./Component/tools/xamarin-component.exe")).Does (() => 
{
	if (!DirectoryExists ("./Component/tools/"))
		CreateDirectory ("./Component/tools/");

	DownloadFile ("https://components.xamarin.com/submit/xpkg", "./Component/tools/tools.zip");

	Unzip ("./Component/tools/tools.zip", "./Component/tools/");

	DeleteFile ("./Component/tools/tools.zip");
});

Task ("component").IsDependentOn ("libs").IsDependentOn ("tools").Does (() => 
{
	DeleteFiles ("./Build/**/*.xml");
	
	if (IsRunningOnWindows ())
		StartProcess ("./Component/tools/xamarin-component.exe", new ProcessSettings { Arguments = "package ./" });
	else
		StartProcess ("mono", new ProcessSettings { Arguments = "./Component/tools/xamarin-component.exe package ./" });
});

Task ("nuget").IsDependentOn ("libs").Does (() => 
{
	if (!DirectoryExists ("./Build/nuget/"))
		CreateDirectory ("./Build/nuget");

	NuGetPack ("./ZXing.Net.Mobile.nuspec", new NuGetPackSettings { OutputDirectory = "./Build/nuget/" });	
	NuGetPack ("./ZXing.Net.Mobile.Forms.nuspec", new NuGetPackSettings { OutputDirectory = "./Build/nuget/" });	
});

Task ("release").IsDependentOn ("nuget").IsDependentOn ("component");
Task ("Default").IsDependentOn ("release");

Task ("publish").IsDependentOn ("nuget").IsDependentOn ("component")
	.Does (() => 
{
	if (string.IsNullOrEmpty (version)) {
		Information ("No version specified, not publishing anything.");		
		return;
	}

	var apiKey = TransformTextFile("./.nugetapikey").ToString ().Trim ();

	StartProcess ("nuget", new ProcessSettings { Arguments = "push ./NuGet/ZXing.Net.Mobile." + version + ".nupkg " + apiKey });
	StartProcess ("nuget", new ProcessSettings { Arguments = "push ./NuGet/ZXing.Net.Mobile.Forms." + version + ".nupkg " + apiKey });
});

Task ("stage").IsDependentOn ("nuget").Does (() => 
{
	if (string.IsNullOrEmpty (version)) {
		Information ("No version specified, not publishing anything.");		
		return;
	}

	var apiKey = TransformTextFile("./.mygetapikey").ToString ().Trim ();

	StartProcess ("nuget", new ProcessSettings { Arguments = "push ./NuGet/ZXing.Net.Mobile." + version + ".nupkg -Source https://www.myget.org/F/redth/api/v2 " + apiKey });
	StartProcess ("nuget", new ProcessSettings { Arguments = "push ./NuGet/ZXing.Net.Mobile.Forms." + version + ".nupkg -Source https://www.myget.org/F/redth/api/v2 " + apiKey });
});

Task ("clean").Does (() => 
{

	CleanDirectory ("./Component/tools/");

	CleanDirectories ("./Build/");

	CleanDirectories ("./**/bin");
	CleanDirectories ("./**/obj");
});

RunTarget (target);
