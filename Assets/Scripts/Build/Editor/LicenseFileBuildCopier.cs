using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// 빌드가 끝나면 배포 시 함께 넣어야 하는 라이선스 파일(글꼴 OFL 등)을 빌드 출력 폴더의 Licenses 폴더로 복사하는 빌드 후처리기.
public class LicenseFileBuildCopier : IPostprocessBuildWithReport
{
    // 빌드 출력 폴더 안에 라이선스 파일을 모아 둘 하위 폴더 이름
    private const string k_LicenseFolderName = "Licenses";

    // 빌드에 함께 넣을 라이선스 파일들(프로젝트 루트 기준 경로)
    private static readonly string[] s_LicenseFiles =
    {
        "Assets/UI/Fonts/Test/BMHANNA_11YRS_OFL.txt",
    };

    public int callbackOrder => 0;

    // 빌드가 성공하지 않았으면 건너뛰고, 성공했으면 라이선스 파일을 출력 폴더로 복사한다.
    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.result == BuildResult.Failed || report.summary.result == BuildResult.Cancelled)
            return;

        string targetFolder = Path.Combine(GetBuildRootFolder(report.summary.outputPath), k_LicenseFolderName);
        Directory.CreateDirectory(targetFolder);

        foreach (string licenseFile in s_LicenseFiles)
            CopyLicenseFile(licenseFile, targetFolder);
    }

    // 빌드 출력 경로에서 라이선스 폴더를 둘 기준 폴더를 구한다. 실행 파일·앱 번들처럼 파일 경로면 그 상위 폴더를, 폴더 경로면 그 폴더를 쓴다.
    private static string GetBuildRootFolder(string outputPath)
    {
        bool isFileLike = File.Exists(outputPath) || Path.HasExtension(outputPath);
        return isFileLike ? Path.GetDirectoryName(outputPath) : outputPath;
    }

    // 라이선스 파일 하나를 대상 폴더로 덮어쓰며 복사하고, 원본이 없으면 경고를 남긴다.
    private static void CopyLicenseFile(string sourcePath, string targetFolder)
    {
        if (!File.Exists(sourcePath))
        {
            Debug.LogWarning($"[LicenseFileBuildCopier] 라이선스 파일을 찾을 수 없어 빌드에 넣지 못했습니다: {sourcePath}");
            return;
        }

        string destinationPath = Path.Combine(targetFolder, Path.GetFileName(sourcePath));
        File.Copy(sourcePath, destinationPath, true);
        Debug.Log($"[LicenseFileBuildCopier] 라이선스 파일을 빌드에 넣었습니다: {destinationPath}");
    }
}
