{
	inputs = {
		nixpkgs.url = "github:nixos/nixpkgs/nixos-unstable";
		systems.url = "github:nix-systems/default";
	};

	outputs = { nixpkgs, systems, ... }: let
		forAllSystems = fn: nixpkgs.lib.genAttrs (import systems) (system: fn nixpkgs.legacyPackages.${system});
	in {
		devShells = forAllSystems (pkgs: {
			default = let
				sdk = pkgs.dotnet-sdk_8;
			in pkgs.mkShell {
				packages = with pkgs; [
					lua52Packages.lua
					luau
					sdk
				];

				DOTNET_ROOT = "${sdk.unwrapped}/share/dotnet";
			};
		});
	};
}
