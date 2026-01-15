#!/usr/bin/env node
/**
 * Solana Program Deployment Script using @solana/web3.js
 * Complete BPF Loader implementation with correct instruction format
 */

const { 
    Connection, 
    Keypair, 
    PublicKey, 
    SystemProgram,
    Transaction,
    sendAndConfirmTransaction,
    TransactionInstruction,
    SYSVAR_RENT_PUBKEY,
    SYSVAR_CLOCK_PUBKEY
} = require('@solana/web3.js');
const fs = require('fs');

// BPF Loader Upgradeable Program ID
const BPF_LOADER_UPGRADEABLE_PROGRAM_ID = new PublicKey('BPFLoaderUpgradeab1e11111111111111111111111');

// Parse command line arguments
const programPath = process.argv[2] || '/tmp/final_test/target/deploy/rust_main_template.so';
const payerKeypairPath = process.argv[3] || '/tmp/new_solana_keypair.json';
const programKeypairPath = process.argv[4] || '/tmp/final_test/target/deploy/rust_main_template-keypair.json';
const rpcUrl = process.argv[5] || 'https://api.devnet.solana.com';

console.log('🚀 Deploying Solana program using @solana/web3.js...\n');
console.log('Program:', programPath);
console.log('Payer:', payerKeypairPath);
console.log('Program ID keypair:', programKeypairPath);
console.log('RPC:', rpcUrl);
console.log('');

// Helper function to create initialize buffer instruction
function createInitializeBufferInstruction(bufferPubkey, payerPubkey) {
    // Bincode format: [discriminant (u32)]
    const instructionData = Buffer.alloc(4);
    instructionData.writeUInt32LE(0, 0); // InitializeBuffer instruction discriminator
    
    return new TransactionInstruction({
        keys: [
            { pubkey: bufferPubkey, isSigner: true, isWritable: true },
            { pubkey: payerPubkey, isSigner: true, isWritable: false },
        ],
        programId: BPF_LOADER_UPGRADEABLE_PROGRAM_ID,
        data: instructionData,
    });
}

// Helper function to create write buffer instruction (correct bincode format)
function createWriteBufferInstruction(bufferPubkey, payerPubkey, offset, data) {
    // Bincode format: [discriminant (u32), offset (u32), bytes_len (u64), bytes...]
    const instructionData = Buffer.alloc(4 + 4 + 8 + data.length);
    let offset_pos = 0;
    
    // Instruction discriminant: 1 (Write)
    instructionData.writeUInt32LE(1, offset_pos);
    offset_pos += 4;
    
    // Offset: u32
    instructionData.writeUInt32LE(offset, offset_pos);
    offset_pos += 4;
    
    // Bytes length: u64
    const bytesLen = BigInt(data.length);
    instructionData.writeBigUInt64LE(bytesLen, offset_pos);
    offset_pos += 8;
    
    // Bytes data
    data.copy(instructionData, offset_pos);
    
    return new TransactionInstruction({
        keys: [
            { pubkey: bufferPubkey, isSigner: false, isWritable: true },
            { pubkey: payerPubkey, isSigner: true, isWritable: false },
        ],
        programId: BPF_LOADER_UPGRADEABLE_PROGRAM_ID,
        data: instructionData,
    });
}

// Helper function to create deploy instruction with correct account order
function createDeployInstruction(programId, bufferPubkey, upgradeAuthority, payerPubkey, maxDataLen) {
    // Bincode format: [discriminant (u32), max_data_len (u64)]
    const instructionData = Buffer.alloc(4 + 8);
    instructionData.writeUInt32LE(2, 0); // DeployWithMaxDataLen instruction discriminator
    const maxDataLenBigInt = BigInt(maxDataLen);
    instructionData.writeBigUInt64LE(maxDataLenBigInt, 4);
    
    // Derive programdata address (PDA)
    // According to Solana source: ProgramData is derived from [program_id] as seed
    // with BPF_LOADER_UPGRADEABLE_PROGRAM_ID as the program
    const [programDataAddress] = PublicKey.findProgramAddressSync(
        [programId.toBuffer()],
        BPF_LOADER_UPGRADEABLE_PROGRAM_ID
    );
    
    console.log(`   ProgramData address: ${programDataAddress.toBase58()}`);
    
    // Account order for DeployWithMaxDataLen (from Solana source code):
    // 0. Payer (signer, writable) - funds ProgramData account creation
    // 1. ProgramData account (writable) - uninitialized
    // 2. Program account (writable, signer) - uninitialized
    // 3. Buffer account (writable) - contains program data
    // 4. Rent Sysvar (read-only)
    // 5. Clock Sysvar (read-only)
    // 6. System Program (read-only)
    // 7. Upgrade authority (signer) - program's authority
    return new TransactionInstruction({
        keys: [
            { pubkey: payerPubkey, isSigner: true, isWritable: true },
            { pubkey: programDataAddress, isSigner: false, isWritable: true },
            { pubkey: programId, isSigner: true, isWritable: true },
            { pubkey: bufferPubkey, isSigner: false, isWritable: true },
            { pubkey: SYSVAR_RENT_PUBKEY, isSigner: false, isWritable: false },
            { pubkey: SYSVAR_CLOCK_PUBKEY, isSigner: false, isWritable: false },
            { pubkey: SystemProgram.programId, isSigner: false, isWritable: false },
            { pubkey: upgradeAuthority, isSigner: true, isWritable: false },
        ],
        programId: BPF_LOADER_UPGRADEABLE_PROGRAM_ID,
        data: instructionData,
    });
}

async function deploy() {
    try {
        // Connect to Solana cluster
        const connection = new Connection(rpcUrl, 'confirmed');
        console.log('✅ Connected to Solana cluster\n');

        // Read payer keypair
        const payerKeypairBytes = JSON.parse(fs.readFileSync(payerKeypairPath, 'utf-8'));
        const payer = Keypair.fromSecretKey(Buffer.from(payerKeypairBytes));
        console.log('✅ Loaded payer keypair:', payer.publicKey.toBase58());

        // Check payer balance
        const balance = await connection.getBalance(payer.publicKey);
        const balanceSOL = balance / 1e9;
        console.log(`   Balance: ${balanceSOL} SOL\n`);

        if (balanceSOL < 0.1) {
            throw new Error(`Insufficient balance: ${balanceSOL} SOL. Need at least 0.1 SOL for deployment.`);
        }

        // Read program keypair (for program ID)
        let programKeypair;
        let programId;
        
        if (fs.existsSync(programKeypairPath)) {
            const programKeypairBytes = JSON.parse(fs.readFileSync(programKeypairPath, 'utf-8'));
            programKeypair = Keypair.fromSecretKey(Buffer.from(programKeypairBytes));
            programId = programKeypair.publicKey;
            console.log('✅ Loaded program keypair:', programId.toBase58());
        } else {
            // Generate new program keypair if it doesn't exist
            programKeypair = Keypair.generate();
            programId = programKeypair.publicKey;
            console.log('✅ Generated new program keypair:', programId.toBase58());
        }

        // Read program binary
        const programBuffer = fs.readFileSync(programPath);
        console.log(`✅ Loaded program binary: ${programBuffer.length} bytes\n`);

        // Deploy using BPF Loader Upgradeable
        console.log('📦 Deploying program using BPF Loader...');
        console.log('   This may take 1-2 minutes...\n');

        // Step 1: Create and initialize buffer account
        const bufferKeypair = Keypair.generate();
        const bufferPubkey = bufferKeypair.publicKey;
        
        // Calculate buffer size (program size + overhead for buffer account structure)
        const bufferSize = programBuffer.length + 50; // Add padding for buffer account overhead
        
        // Get minimum balance for rent exemption
        const minBalanceForBuffer = await connection.getMinimumBalanceForRentExemption(bufferSize);
        console.log(`   Buffer size: ${bufferSize} bytes`);
        console.log(`   Buffer address: ${bufferPubkey.toBase58()}`);
        console.log(`   Minimum balance: ${minBalanceForBuffer / 1e9} SOL\n`);

        // Create buffer account
        const createBufferIx = SystemProgram.createAccount({
            fromPubkey: payer.publicKey,
            newAccountPubkey: bufferPubkey,
            space: bufferSize,
            lamports: minBalanceForBuffer,
            programId: BPF_LOADER_UPGRADEABLE_PROGRAM_ID,
        });

        // Initialize buffer
        const initBufferIx = createInitializeBufferInstruction(bufferPubkey, payer.publicKey);

        // Combine create and initialize in one transaction
        console.log('   Creating and initializing buffer account...');
        const createInitTx = new Transaction().add(createBufferIx, initBufferIx);
        const createInitSig = await sendAndConfirmTransaction(
            connection,
            createInitTx,
            [payer, bufferKeypair],
            { commitment: 'confirmed', skipPreflight: false }
        );
        console.log(`   ✅ Buffer created and initialized: ${createInitSig}\n`);

        // Step 2: Write program data to buffer (in chunks)
        const maxChunkSize = 900; // Conservative chunk size
        const chunks = [];
        for (let i = 0; i < programBuffer.length; i += maxChunkSize) {
            const chunk = programBuffer.slice(i, i + maxChunkSize);
            chunks.push(chunk);
        }

        console.log(`   Writing ${chunks.length} chunk(s) to buffer...`);

        // Write chunks to buffer
        for (let i = 0; i < chunks.length; i++) {
            const chunk = chunks[i];
            const offset = i * maxChunkSize;
            
            // Create write instruction with correct bincode format
            const writeIx = createWriteBufferInstruction(
                bufferPubkey,
                payer.publicKey,
                offset,
                chunk
            );
            
            const writeTx = new Transaction().add(writeIx);
            const writeSig = await sendAndConfirmTransaction(
                connection,
                writeTx,
                [payer],
                { commitment: 'confirmed', skipPreflight: false }
            );
            console.log(`   ✅ Wrote chunk ${i + 1}/${chunks.length}: ${writeSig}`);
        }

        // Step 3: Verify buffer was written correctly
        console.log('\n   Verifying buffer contents...');
        const bufferInfo = await connection.getAccountInfo(bufferPubkey);
        if (bufferInfo) {
            console.log(`   ✅ Buffer account exists: ${bufferInfo.data.length} bytes`);
            if (bufferInfo.data.length < programBuffer.length) {
                throw new Error(`Buffer size mismatch: expected ${programBuffer.length}, got ${bufferInfo.data.length}`);
            }
        } else {
            throw new Error('Buffer account not found after writing');
        }

        // Step 4: Deploy from buffer to program
        // DeployWithMaxDataLen will create both Program and ProgramData accounts automatically
        console.log('\n   Deploying program from buffer...');
        
        // max_data_len needs to be at least the program size, but should be larger for future upgrades
        // Use a reasonable multiple of the program size
        const maxDataLen = Math.max(programBuffer.length * 2, programBuffer.length + 1024);
        console.log(`   Max data length: ${maxDataLen} bytes (program: ${programBuffer.length} bytes)`);
        
        const deployIx = createDeployInstruction(
            programId,
            bufferPubkey,
            payer.publicKey, // upgrade authority
            payer.publicKey, // payer
            maxDataLen
        );

        const deployTx = new Transaction().add(deployIx);
        const deploySig = await sendAndConfirmTransaction(
            connection,
            deployTx,
            [payer, programKeypair],
            { commitment: 'confirmed', skipPreflight: false }
        );

        console.log(`   ✅ Program deployed: ${deploySig}\n`);

        // Verify deployment
        const programInfo = await connection.getAccountInfo(programId);
        if (programInfo && programInfo.executable) {
            console.log('✅✅✅ DEPLOYMENT SUCCESSFUL!');
            console.log(`   Program ID: ${programId.toBase58()}`);
            console.log(`   Transaction: ${deploySig}`);
            console.log(`   Wallet: ${payer.publicKey.toBase58()}`);
            console.log(`\n📍 View on Solana Explorer:`);
            console.log(`   https://explorer.solana.com/address/${programId.toBase58()}?cluster=devnet`);
            console.log(`   https://explorer.solana.com/tx/${deploySig}?cluster=devnet`);
        } else {
            throw new Error('Program deployed but not marked as executable');
        }

    } catch (error) {
        console.error('\n❌ Deployment failed:', error.message);
        if (error.stack) {
            console.error('Stack trace:', error.stack);
        }
        if (error.logs) {
            console.error('Transaction logs:', error.logs);
        }
        process.exit(1);
    }
}

deploy();
