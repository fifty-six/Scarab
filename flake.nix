{
  description = "A mod installer for Hollow Knight";

  inputs = {
    flake-parts.url = "github:hercules-ci/flake-parts";
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
  };

  outputs = inputs @ {flake-parts, ...}:
    flake-parts.lib.mkFlake {inherit inputs;} {
      imports = [];
      systems = ["x86_64-linux" "aarch64-linux" "aarch64-darwin" "x86_64-darwin"];
      perSystem = {
        config,
        self',
        inputs',
        pkgs,
        system,
        ...
      }: let
        csproj = builtins.readFile ./Scarab/Scarab.csproj;
        versionS = builtins.match ".*<Version>(.*?)</Version>.*" csproj;
        version = builtins.elemAt versionS 0;

        scarab = pkgs.buildDotnetModule {
          pname = "Scarab";
          version = version;

          src = ./.;

          projectFile = "Scarab/Scarab.csproj";
          nugetDeps = ./deps.json;

          meta = {
            description = "Hollow Knight mod installer";
            homepage = "https://github.com/fifty-six/Scarab";
            downloadPage = "https://github.com/fifty-six/Scarab/releases/tag/v${version}";
            changelog = "https://github.com/fifty-six/Scarab/releases/tag/v${version}";
            license = pkgs.lib.licenses.gpl3Only;
            mainProgram = "Scarab";
            platforms = pkgs.lib.platforms.linux;
          };
        };
      in {
        devShells.default = pkgs.mkShellNoCC {
          packages = with pkgs.dotnetCorePackages; [
            (combinePackages [
              sdk_8_0
            ])
          ];
        };

        packages.default = scarab;

        packages.debug = scarab.overrideAttrs (self: super: {
          # not buildType because this is later in the derivation I think?
          dotnetBuildType = "Debug";
        });
      };
      flake = {
      };
    };
}
