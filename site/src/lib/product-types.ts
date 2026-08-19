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
  /** CommandLine.HelpText with its verb constants resolved, one entry per line. */
  help: string[];
}
