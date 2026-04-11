class Gauntletci < Formula
  desc "Deterministic pre-commit risk detection engine for git diffs"
  homepage "https://github.com/EricCogen/GauntletCI"
  version "2.0.0"
  license "Elastic-2.0"

  on_macos do
    on_arm do
      url "https://github.com/EricCogen/GauntletCI/releases/download/v#{version}/gauntletci-osx-arm64.tar.gz"
      sha256 "0000000000000000000000000000000000000000000000000000000000000000"
    end
    on_intel do
      url "https://github.com/EricCogen/GauntletCI/releases/download/v#{version}/gauntletci-osx-x64.tar.gz"
      sha256 "0000000000000000000000000000000000000000000000000000000000000000"
    end
  end

  on_linux do
    on_arm do
      url "https://github.com/EricCogen/GauntletCI/releases/download/v#{version}/gauntletci-linux-arm64.tar.gz"
      sha256 "0000000000000000000000000000000000000000000000000000000000000000"
    end
    on_intel do
      url "https://github.com/EricCogen/GauntletCI/releases/download/v#{version}/gauntletci-linux-x64.tar.gz"
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
