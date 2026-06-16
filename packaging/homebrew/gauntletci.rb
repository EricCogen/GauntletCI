class Gauntletci < Formula
  desc "Deterministic pre-commit risk detection engine for git diffs"
  homepage "https://gauntletci.com"
  version "2.8.1"
  license "Elastic-2.0"

  # SHA256 values are updated automatically via the update-homebrew-tap.yml
  # workflow after each release. Do not edit by hand.
  on_macos do
    on_arm do
      url "https://github.com/EricCogen/GauntletCI/releases/download/v#{version}/gauntletci-osx-arm64-#{version}.tar.gz"
      sha256 "939d4916fc9c4f56dddb155fb6a7564f7643ad6fe4e18ba3eb93467a91d20418"
    end
    on_intel do
      url "https://github.com/EricCogen/GauntletCI/releases/download/v#{version}/gauntletci-osx-x64-#{version}.tar.gz"
      sha256 "71d92ae3c08a39b37f1a71de425ff82fd6f0419485e591f586b830eb4f678073"
    end
  end

  on_linux do
    on_arm do
      url "https://github.com/EricCogen/GauntletCI/releases/download/v#{version}/gauntletci-linux-arm64-#{version}.tar.gz"
      sha256 "e71be039ba2cbc5e3690cafff9b5f54135ed32c68d156822975ff0df0c3d3c15"
    end
    on_intel do
      url "https://github.com/EricCogen/GauntletCI/releases/download/v#{version}/gauntletci-linux-x64-#{version}.tar.gz"
      sha256 "a84a9d54b9ae7aceb97cfb9cb352af46b1d9e757314ac48947ed9f594e525c45"
    end
  end

  def install
    bin.install "gauntletci"
  end

  test do
    assert_match "GauntletCI", shell_output("#{bin}/gauntletci --version")
  end
end
