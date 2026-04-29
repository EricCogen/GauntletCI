class Gauntletci < Formula
  desc "Deterministic pre-commit risk detection engine for git diffs"
  homepage "https://gauntletci.com"
  version "2.1.0"
  license "Elastic-2.0"

  # SHA256 values are updated automatically via the update-homebrew-tap.yml
  # workflow after each release. Do not edit by hand.
  on_macos do
    on_arm do
      url "https://github.com/EricCogen/GauntletCI/releases/download/v#{version}/gauntletci-osx-arm64-#{version}.tar.gz"
      sha256 "0000000000000000000000000000000000000000000000000000000000000000"
    end
    on_intel do
      url "https://github.com/EricCogen/GauntletCI/releases/download/v#{version}/gauntletci-osx-x64-#{version}.tar.gz"
      sha256 "0000000000000000000000000000000000000000000000000000000000000000"
    end
  end

  on_linux do
    on_arm do
      url "https://github.com/EricCogen/GauntletCI/releases/download/v#{version}/gauntletci-linux-arm64-#{version}.tar.gz"
      sha256 "0000000000000000000000000000000000000000000000000000000000000000"
    end
    on_intel do
      url "https://github.com/EricCogen/GauntletCI/releases/download/v#{version}/gauntletci-linux-x64-#{version}.tar.gz"
      sha256 "0000000000000000000000000000000000000000000000000000000000000000"
    end
  end

  def install
    bin.install "gauntletci"
  end

  test do
    assert_match "GauntletCI", shell_output("#{bin}/gauntletci --version")
  end
end
