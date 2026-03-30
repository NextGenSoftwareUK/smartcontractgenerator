# Q2P Shop OAPP Template

This folder is the source for the Q2P OAPP template. When published via STAR CLI, it becomes a `.oapptemplate` file that builders can use to spin up new virtual shops.

## Structure

- `OAPPTemplateDNA.json` - Template metadata
- `storefront/` - Symlink or copy of the Next.js storefront
- `medusa/` - Symlink or copy of the Medusa backend

## Publishing

```bash
cd "STAR ODK/NextGenSoftware.OASIS.STAR.CLI"
star oapp template publish
# Select this template when prompted
```

## Creating a Shop from Template

```bash
star create oapp "My Q2P Shop" --template q2p-shop
# Or use the Light Wizard: oapp create
```

## Template Tags

When STAR.Light applies this template, these tags are replaced:
- `{OAPPNAME}` - Shop name
- `{OAPPNAMESPACE}` - C# namespace (if applicable)
- `[[SITE_NAME]]` - Custom: site display name
