@echo off

IF "%SITE_FLAVOR%" == "react" (
  deploy.react.cmd
) ELSE (
  IF "%SITE_FLAVOR%" == "functions" (
    deploy.functions.cmd
  ) ELSE (
    echo You have to set SITE_FLAVOR setting to either "react" or "functions"
    exit /b 1
  )
)