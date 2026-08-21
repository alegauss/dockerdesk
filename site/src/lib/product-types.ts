// The shape of the generated product-facts module (DD159). product.generated.ts is written by
// scripts/product.mjs from four committed source files, and it is the only source the copy
// reads a count from — a number about how this product is built is generated or it is not on
// the page.

export interface ProductData {
  /** What a provision does, read off the ProvisioningStep enum. */
  provisioning: {
    /** How many steps there are — the same number installer.iss draws a bar for. */
    steps: number;
    /** The steps that acquire an artefact, named by what they acquire: "rootfs", "cli", … */
    acquire: string[];
  };
  /** What the build pins, read off engine-manifest.json. */
  artefacts: {
    count: number;
    /** Upstream version per artefact id. */
    versions: Record<string, string>;
    /** Every host an artefact is downloaded from, in manifest order and deduplicated. */
    hosts: string[];
  };
  /** What a preflight reports, read off PreflightInspection.Rows. */
  preflight: {
    /** The row ids, in report order: "windows-build", "virtualization", … */
    rows: string[];
  };
  /** What the tray's context menu is, read off TrayMenu (DD160). */
  tray: {
    /** Every item in the order the strip is built, separators excluded. */
    items: { caption: string; hidden: boolean }[];
    /** How many of them a menu a user opens actually shows. */
    visible: number;
  };
  /** What the window's nav strip is, read off MainWindow.xaml (DD165). */
  window: {
    /** Every destination, in the order the strip shows them. */
    destinations: string[];
    /**
     * How many of them are views of the machine — every destination but About.
     *
     * The number the window section states. About is a destination and is not one of these:
     * the sentence is about what the tool shows you of your machine, and the terms page is not
     * that.
     */
    machine: number;
  };
  /** CommandLine.HelpText with its verb constants resolved, one entry per line. */
  help: string[];
}
