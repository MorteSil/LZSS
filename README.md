# LZSS

/********************************************
 * This requires Pointers to efficiently 
 * operate so it must be marked as unsafe.
 * The cost to refactor it into safe code
 * decreases the performance considerably
 * and introuduces significant overhead.
 * 
 * If you absolutely cannot use unmanaged code,
 * you will need to create a reference
 * object (Class) to hold the buffers and
 * implement a function to track the pointer
 * location within the buffers. The Look Ahead
 * functionality will require constant calls
 * to update the data in the reference class
 *********************************************/


Implements Compression and Decompression for data using the LZSS Compression Scheme.
Compression Usage: byte[] output = LZSS.Compress(byte[] rawData);
Decompression Usage: byte[] output = LZSS.Decompress(byte[] compressedData, int targetDecompressedSize);
