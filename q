[33mcommit e40a399bfcda4d0f1cb5ecb14e704bef4e802865[m[33m ([m[1;36mHEAD[m[33m -> [m[1;32mmain[m[33m, [m[1;31morigin/main[m[33m, [m[1;31morigin/HEAD[m[33m)[m
Author: nazarovqurbonali <nazarovqurbonali4@gmail.com>
Date:   Thu Sep 4 13:35:41 2025 +0500

    refactor: reorganize project structure with shared components
    
    - Move common DTOs, extensions, and utilities to Shared folder for better reusability
    - Add ProcessExtensions for universal process execution across all blockchain platforms
    - Restructure validation, helpers, and resources under Shared namespace
    - Create dedicated Services folder for Ethereum contract operations
    - Improve code organization and maintainability for multi-blockchain support

[33mcommit 9217ced773b6336085bfcb661399a2ac5a72bb62[m
Author: nazarovqurbonali <nazarovqurbonali4@gmail.com>
Date:   Thu Sep 4 11:15:10 2025 +0500

    refactoring: add check to EthereumContractGenerate/GenerateAsync for JSON file validity.

[33mcommit 6a845445d2ba0f0519f7752921189dc87207babf[m
Author: nazarovqurbonali <nazarovqurbonali4@gmail.com>
Date:   Wed Sep 3 20:15:47 2025 +0500

    Add new interfaces, templates, and helpers for smart contract generation

[33mcommit 739b4e78806cb5788f53b44fc9742d3a76fdc638[m
Author: nazarovqurbonali <nazarovqurbonali4@gmail.com>
Date:   Tue Sep 2 20:05:25 2025 +0500

    refactor: restructure DTOs by blockchain and create contract interfaces
    
    - Separated DTOs into blockchain-specific responses (Ethereum, Solana, Radix)
    - Created base contract interfaces for compile, deploy, and generate operations
    - Implemented inheritance structure with BaseCompileContractResponse
    - Added blockchain-specific response classes:
      * EthereumCompileContractResponse (with ABI + Bytecode)
      * SolanaCompileContractResponse (with .so file)
      * RadixCompileContractResponse (with .wasm file)
    - Defined contract interfaces for each blockchain platform
    - Improved code organization and separation of concerns

[33mcommit aac8133c9dbdc475c0c55c287e013eb6348063e8[m
Author: nazarovqurbonali <nazarovqurbonali4@gmail.com>
Date:   Tue Sep 2 17:15:31 2025 +0500

    refactor: migrate to Vertical Slice architecture and restructure solution
    
    - reorganized solution structure to follow Vertical Slice architecture
    - created new project  with core shared components
    - introduced Result pattern for consistent operation outcomes
    - enhanced logging infrastructure with HttpContext extensions
      (user id, user agent, remote IP, correlation id)
    - added middleware and cross-cutting utilities

[33mcommit 8c5d58055f3914c3edc60179b7252483dbb5cf6e[m
Author: nazarovqurbonali <nazarovqurbonali4@gmail.com>
Date:   Mon Sep 1 17:49:09 2025 +0500

    feat: Complete Scrypto smart contract services implementation
    
    - Implement ScryptoContractCompiler with Docker-based compilation
      * Support for WASM compilation using ghcr.io/krulknul/try-scrypto:1.3.0
      * Cargo cache management for improved build performance
      * Automatic file detection (.wasm and .rpd/.schema files)
      * Comprehensive error handling and logging
    
    - Implement ScryptoContractDeployer with Resim integration
      * Docker-based deployment using Radix Engine Simulator
      * Automatic account creation and management
      * Package publishing with address extraction
      * Single-container execution for state persistence
    
    - Create comprehensive Scrypto template system
      * Complete Handlebars template covering all Scrypto features
      * Support for blueprints, events, enums, structs, methods
      * Authorization, resources, NFTs, and utility functions
      * Test and benchmark generation capabilities
    
    - Add Radix configuration options
      * RadixOptions class for deployment settings
      * Network URL and simulator configuration
      * Account management settings
    
    - Integrate services with existing infrastructure
      * Proper dependency injection setup
      * Consistent error handling patterns
      * Comprehensive logging throughout
    
    Author: nazarovqurbonali
    Date: 2025-09-01 12:48:12 UTC
    Status: Services ready for production use

[33mcommit f323c30be4a079da82cec379e4b958de5fbc4f57[m
Author: nazarovqurbonali <nazarovqurbonali4@gmail.com>
Date:   Mon Sep 1 14:11:23 2025 +0500

    refactoring: change template scrypto.

[33mcommit b365b45c835b21b0e4aa3a04b2140c9f366a9bdf[m
Author: nazarovqurbonali <nazarovqurbonali4@gmail.com>
Date:   Sun Aug 31 19:43:38 2025 +0500

    bugfix: change template scrypto.

[33mcommit 2f360a10789a67aa177a80950be418d41eec8bbe[m
Author: nazarovqurbonali <nazarovqurbonali4@gmail.com>
Date:   Sat Aug 30 17:12:13 2025 +0500

    feature add base interface scrypto

[33mcommit 6f1006f1ffd807222ca02c9a3a0e5161240d3e8e[m
Author: nazarovqurbonali <nazarovqurbonali4@gmail.com>
Date:   Sat Aug 30 17:11:13 2025 +0500

    feature: add template scrypto

[33mcommit 51511e02a63d2ba2fe117bd015dc9d882d43c347[m
Author: nazarovqurbonali <nazarovqurbonali4@gmail.com>
Date:   Mon Aug 25 19:02:14 2025 +0500

    delete other files

[33mcommit 3185cdc8a4b390dda80dbcb5d6c887ea1d9cda8c[m
Author: nazarovqurbonali <nazarovqurbonali4@gmail.com>
Date:   Mon Aug 25 19:01:27 2025 +0500

    testing: finish successfully

[33mcommit b7952367dca1edbec7b7e009b3532ae9a2542afe[m
Author: nazarovqurbonali <nazarovqurbonali4@gmail.com>
Date:   Mon Aug 25 18:06:51 2025 +0500

    refactoring: delete other files

[33mcommit 95288793b2106b5abc6d0d1e0a50fd0a3d21c9fb[m
Author: nazarovqurbonali <nazarovqurbonali4@gmail.com>
Date:   Mon Aug 25 18:06:03 2025 +0500

    feature: impl-solana-deploy-sc

[33mcommit c1d14b5c849807c2a3f9315cb9e0f526e91c5e71[m
Author: nazarovqurbonali <nazarovqurbonali4@gmail.com>
Date:   Mon Aug 25 16:28:26 2025 +0500

    feature: impl-solana-compile-sc

[33mcommit 4de8a0577c2f959af4ce6b959cfa5cd4e8d64722[m
Author: nazarovqurbonali <nazarovqurbonali4@gmail.com>
Date:   Mon Aug 25 15:54:16 2025 +0500

    feature: impl-solana-gen-sc

[33mcommit 9030c21a6a1a3b3f76a727cfbfb6b95c5a53ff81[m
Author: nazarovqurbonali <nazarovqurbonali4@gmail.com>
Date:   Mon Aug 25 10:53:27 2025 +0500

    refactoring: delete unnecessary files

[33mcommit 8dd0587ace7a06b8b1e47697a254a5aa1c790d5e[m
Author: nazarovqurbonali <nazarovqurbonali4@gmail.com>
Date:   Thu Aug 21 12:26:48 2025 +0500

    feature: add rust contract and register to DI container.

[33mcommit cdd3b4f5f50a1f102dc3834838a82ca22244d144[m
Author: nazarovqurbonali <nazarovqurbonali4@gmail.com>
Date:   Thu Aug 21 12:12:19 2025 +0500

    feature: create template for rust-anchor.

[33mcommit 30951826e41aed9fb2c4e4cf8a2058190e5b9646[m
Author: nazarovqurbonali <nazarovqurbonali4@gmail.com>
Date:   Mon Aug 18 10:35:36 2025 +0500

    refactoring: change address api .

[33mcommit 6f662d2245fc02c0b7ae243fc30143d0b4646331[m
Author: nazarovqurbonali <nazarovqurbonali4@gmail.com>
Date:   Tue Aug 12 18:04:23 2025 +0500

    feature: finish mvp  impl-gen-sm for soldity

[33mcommit cd164a5dcc00b8333e32fee3189b75bd3342e4ba[m
Author: nazarovqurbonali <nazarovqurbonali4@gmail.com>
Date:   Tue Aug 12 18:03:25 2025 +0500

    feature: impl-soldity-deploy-sc

[33mcommit a5116233e4e9684c92f077ab66422449a294f937[m
Author: nazarovqurbonali <nazarovqurbonali4@gmail.com>
Date:   Tue Aug 12 17:24:17 2025 +0500

    feature: impl-sol-compile-sc

[33mcommit 22a2301966bf8298696d21fe883f52db094f3d9c[m
Author: Qurbonali Nazarov <nazarovqurbonali4@gmail.com>
Date:   Tue Aug 12 15:57:28 2025 +0500

    Delete README.md

[33mcommit 25778136c1e4f810701848296e0c0e452c0f718b[m
Author: nazarovqurbonali <nazarovqurbonali4@gmail.com>
Date:   Tue Aug 12 15:56:26 2025 +0500

    add README

[33mcommit 93a9faa07f450f81a0915f591598db08eb8dd8fb[m
Author: nazarovqurbonali <nazarovqurbonali4@gmail.com>
Date:   Tue Aug 12 15:51:50 2025 +0500

    feature: impl-solidity-gen-sc v-1.

[33mcommit 236f13ebd4cf7e4ad443d758627df00ba8546db6[m
Author: nazarovqurbonali <nazarovqurbonali4@gmail.com>
Date:   Sun Aug 10 17:10:31 2025 +0500

    feature: add structure solidity.

[33mcommit 80d9121ccd30639c880cd14897e27ed8955c0cc8[m
Author: nazarovqurbonali <nazarovqurbonali4@gmail.com>
Date:   Sun Aug 10 16:12:40 2025 +0500

    feature: add readme file.

[33mcommit dcf210cc81dea15ef20c175c3cadba048f1e5e76[m
Author: nazarovqurbonali <nazarovqurbonali4@gmail.com>
Date:   Sun Aug 10 16:08:57 2025 +0500

    init
