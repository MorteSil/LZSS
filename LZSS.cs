using System.Runtime.InteropServices;

namespace LZSS
{
    /// <summary>
    /// Implements Compression and Decompression for data using the LZSS Compression.
    /// Compression Usage: byte[] output = LZSS.Compress(byte[] rawData);
    /// Decompression Usage: byte[] output = LZSS.Decompress(byte[] compressedData, int targetDecompressedSize);
    /// </summary>
    public static class Lzss
    {
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

        private const int INDEX_BIT_COUNT = 12;
        private const int LENGTH_BIT_COUNT = 4;
        private const int WINDOW_SIZE = 0x1000;
        private const int RAW_LOOK_AHEAD_SIZE = 0x10;
        private const int BREAK_EVEN = 1;
        private const int LOOK_AHEAD_SIZE = 0x11;
        private const int TREE_ROOT = 0x1000;
        private const int END_OF_STREAM = 0;
        private const int UNUSED = 0;

        /// <summary>
        /// Modifies the data in the Buffer Window
        /// </summary>
        /// <param name="A_0">Masked </param>
        /// <returns></returns>
        private static int MOD_WINDOW(int A_0)
        {
            return A_0 & 0xfff;
        }
        /// <summary>
        /// Initializes the Output Buffer (LZSS Compression Context)
        /// </summary>
        /// <param name="CTXT1">The Buffer to Initialize</param>
        private static void InitOutputBuffer(ref LZSS_COMP_CTXT CTXT1)
        {
            CTXT1.DataBuffer[0] = 0;
            CTXT1.FlagBitMask = 1;
            CTXT1.OldBufferOffset = CTXT1.BufferOffset;
            CTXT1.BufferOffset = 1;
        }
        /// <summary>
        /// Inputs data from a string into a buffer for comparison and potential compression
        /// </summary>
        /// <param name="input_string"></param>
        /// <param name="CTXT1"></param>
        /// <returns></returns>
        private static unsafe int InputBit(byte* input_string, ref LZSS_COMP_CTXT CTXT1)
        {
            CTXT1.include_input_string = false;
            if (CTXT1.FlagBitMask == 0x100)
            {
                InitInputBuffer(input_string, ref CTXT1);
                CTXT1.include_input_string = true;
            }
            CTXT1.FlagBitMask = CTXT1.FlagBitMask << 1;
            return CTXT1.DataBuffer[0] & CTXT1.FlagBitMask >> 1;
        }
        /// <summary>
        /// Removes a string segment after it has been replaced with the Compression Scheme representation
        /// </summary>
        /// <param name="p"></param>
        /// <param name="CTXT1"></param>
        private static void DeleteString(int p, ref LZSS_COMP_CTXT CTXT1)
        {
            if (CTXT1.tree[p].parent != UNUSED)
            {
                if (CTXT1.tree[p].larger_child == UNUSED)
                {
                    ContractNode(p, CTXT1.tree[p].smaller_child, ref CTXT1);
                }
                else if (CTXT1.tree[p].smaller_child == UNUSED)
                {
                    ContractNode(p, CTXT1.tree[p].larger_child, ref CTXT1);
                }
                else
                {
                    int replacement = FindNextNode(p, ref CTXT1);
                    DeleteString(replacement, ref CTXT1);
                    ReplaceNode(p, replacement, ref CTXT1);
                }
            }
        }
        /// <summary>
        /// Adds a string to specific location during decompression
        /// </summary>
        /// <param name="new_node"></param>
        /// <param name="match_position"></param>
        /// <param name="CTXT2"></param>
        /// <returns></returns>
        private static int AddString(int new_node, ref int match_position, ref LZSS_COMP_CTXT CTXT2)
        {
            int num = 0;
            int test_node = 0;
            int delta = 0;
            int match_length = 0;
            int child = 0;
            if (new_node == 0)
            {
                return 0;
            }
            test_node = CTXT2.tree[0x1000].larger_child;
            match_length = 0;
            while (true)
            {
                for (num = 0; num < 0x11; num++)
                {
                    delta = CTXT2.window[MOD_WINDOW(new_node + num)] - CTXT2.window[MOD_WINDOW(test_node + num)];
                    if (delta != 0)
                    {
                        break;
                    }
                }
                if (num >= match_length)
                {
                    match_length = num;
                    match_position = test_node;
                    if (match_length >= 0x11)
                    {
                        ReplaceNode(test_node, new_node, ref CTXT2);
                        return match_length;
                    }
                }
                if (delta >= 0)
                {
                    child = CTXT2.tree[test_node].larger_child;
                }
                else
                {
                    child = CTXT2.tree[test_node].smaller_child;
                }
                if (child == 0)
                {
                    child = new_node;
                    CTXT2.tree[new_node].parent = test_node;
                    CTXT2.tree[new_node].larger_child = 0;
                    CTXT2.tree[new_node].smaller_child = 0;
                    return match_length;
                }
                test_node = child;
            }
        }
        /// <summary>
        /// Used in the Look Ahead function to manage which nodes are being analyzed in the tree
        /// </summary>
        /// <param name="old_node"></param>
        /// <param name="new_node"></param>
        /// <param name="CTXT2"></param>
        private static void ReplaceNode(int old_node, int new_node, ref LZSS_COMP_CTXT CTXT2)
        {
            int parent = CTXT2.tree[old_node].parent;
            if (CTXT2.tree[parent].smaller_child == old_node)
            {
                CTXT2.tree[parent].smaller_child = new_node;
            }
            else
            {
                CTXT2.tree[parent].larger_child = new_node;
            }
            CTXT2.tree[new_node] = CTXT2.tree[old_node];
            CTXT2.tree[CTXT2.tree[new_node].smaller_child].parent = new_node;
            CTXT2.tree[CTXT2.tree[new_node].larger_child].parent = new_node;
            CTXT2.tree[old_node].parent = UNUSED;
        }
        /// <summary>
        /// Builds the output buffer in memory
        /// </summary>
        /// <param name="data"></param>
        /// <param name="output_string"></param>
        /// <param name="CTXT2"></param>
        /// <returns></returns>
        private static unsafe int OutputChar(int data, byte* output_string, ref LZSS_COMP_CTXT CTXT2)
        {
            CTXT2.DataBuffer[CTXT2.BufferOffset++] = (byte)data;
            CTXT2.DataBuffer[0] = (byte)(CTXT2.DataBuffer[0] | (byte)CTXT2.FlagBitMask);
            CTXT2.FlagBitMask = CTXT2.FlagBitMask << 1;
            CTXT2.include_output_string = false;
            if (CTXT2.FlagBitMask == 0x100)
            {
                CTXT2.include_output_string = true;
                return FlushOutputBuffer(output_string, ref CTXT2);
            }
            return 1;
        }
        /// <summary>
        /// Expands specific data within a buffer and moves it to the output buffer
        /// </summary>
        /// <param name="input_string"></param>
        /// <param name="output_string"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        private static unsafe int Expand(byte* input_string, byte* output_string, int size)
        {
            LZSS_COMP_CTXT CTXT1 = new LZSS_COMP_CTXT();
            CTXT1.Initialize();
            byte* inputHead = input_string;
            InitInputBuffer(input_string, ref CTXT1);
            input_string++;
            int index = 1;
            while (size != 0)
            {
                byte c;
                if (InputBit(input_string, ref CTXT1) != 0)
                {
                    if (CTXT1.include_input_string)
                    {
                        input_string++;
                    }
                    c = input_string[0];
                    input_string++;
                    output_string[0] = c;
                    output_string++;
                    size--;
                    CTXT1.window[index] = c;
                    index = MOD_WINDOW(index + 1);
                }
                else
                {
                    if (CTXT1.include_input_string)
                    {
                        input_string++;
                    }
                    int match_length = input_string[0];
                    input_string++;
                    int match_position = input_string[0];
                    input_string++;
                    match_position |= (match_length & 15) << 8;
                    match_length = match_length >> 4;
                    match_length += BREAK_EVEN;
                    if (match_length < size)
                    {
                        size -= match_length + 1;
                    }
                    else
                    {
                        size = 0;
                        match_length = size - 1;
                    }
                    for (int i = 0; i <= match_length; i++)
                    {
                        c = CTXT1.window[MOD_WINDOW(match_position + i)];
                        output_string[0] = c;
                        output_string++;
                        CTXT1.window[index] = c;
                        index = MOD_WINDOW(index + 1);
                    }
                }
            }
            ulong input_count = (ulong)((input_string - inputHead) / 1);
            return (int)input_count;
        }
        /// <summary>
        /// The keys used to build the dictionary of compressed values
        /// </summary>
        /// <param name="position"></param>
        /// <param name="length"></param>
        /// <param name="output_string"></param>
        /// <param name="CTXT1"></param>
        /// <returns></returns>
        private static unsafe int OutputPair(int position, int length, byte* output_string, ref LZSS_COMP_CTXT CTXT1)
        {
            CTXT1.DataBuffer[CTXT1.BufferOffset] = (byte)(length << 4);
            CTXT1.DataBuffer[CTXT1.BufferOffset++] = (byte)(CTXT1.DataBuffer[CTXT1.BufferOffset++] | (byte)(position >> 8));
            CTXT1.DataBuffer[CTXT1.BufferOffset++] = (byte)(position & 0xff);
            CTXT1.FlagBitMask = CTXT1.FlagBitMask << 1;
            CTXT1.include_output_string = false;
            if (CTXT1.FlagBitMask == 0x100)
            {
                CTXT1.include_output_string = true;
                return FlushOutputBuffer(output_string, ref CTXT1);
            }
            return 1;
        }
        /// <summary>
        /// Used in the Look Ahead function to search for nodes matching the current Compression Key
        /// </summary>
        /// <param name="node"></param>
        /// <param name="CTXT1"></param>
        /// <returns></returns>
        private static int FindNextNode(int node, ref LZSS_COMP_CTXT CTXT1)
        {
            int next = CTXT1.tree[node].smaller_child;
            while (CTXT1.tree[next].larger_child != 0)
            {
                next = CTXT1.tree[next].larger_child;
            }
            return next;
        }
        /// <summary>
        /// Initializes the input buffer
        /// </summary>
        /// <param name="input_string"></param>
        /// <param name="CTXT1"></param>
        private static unsafe void InitInputBuffer(byte* input_string, ref LZSS_COMP_CTXT CTXT1)
        {
            CTXT1.FlagBitMask = 1;
            CTXT1.DataBuffer[0] = input_string[0];
        }
        /// <summary>
        /// Adds nodes together for analysis during Look Ahead
        /// </summary>
        /// <param name="old_node"></param>
        /// <param name="new_node"></param>
        /// <param name="CTXT2"></param>
        private static void ContractNode(int old_node, int new_node, ref LZSS_COMP_CTXT CTXT2)
        {
            CTXT2.tree[new_node].parent = CTXT2.tree[old_node].parent;
            if (CTXT2.tree[CTXT2.tree[old_node].parent].larger_child == old_node)
            {
                CTXT2.tree[CTXT2.tree[old_node].parent].larger_child = new_node;
            }
            else
            {
                CTXT2.tree[CTXT2.tree[old_node].parent].smaller_child = new_node;
            }
            CTXT2.tree[old_node].parent = UNUSED;
        }
        /// <summary>
        /// Compresses data within a buffer
        /// </summary>
        /// <param name="input_string"></param>
        /// <param name="output_string"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        private static unsafe int Compress(byte* input_string, byte* output_string, int size)
        {
            byte c;
            LZSS_COMP_CTXT CTXT1 = new LZSS_COMP_CTXT();
            CTXT1.Initialize();
            CTXT1.compressed_size = 0;
            CTXT1.original_size = size;
            InitOutputBuffer(ref CTXT1);
            int CurrentPosition = 1;
            int i = 0;
            while (i < LOOK_AHEAD_SIZE)
            {
                c = input_string[0];
                input_string++;
                size--;
                if (size < 0)
                {
                    break;
                }
                CTXT1.window[CurrentPosition + i] = c;
                i++;
            }
            int look_ahead_bytes = i;
            InitTree(CurrentPosition, ref CTXT1);
            int match_length = 0;
            int match_position = 0;
            while (look_ahead_bytes > 0)
            {
                int replace_count;
                if (match_length > look_ahead_bytes)
                {
                    match_length = look_ahead_bytes;
                }
                if (match_length <= BREAK_EVEN)
                {
                    replace_count = 1;
                    if (OutputChar(CTXT1.window[CurrentPosition], output_string, ref CTXT1) == 0)
                    {
                        return 0;
                    }
                    if (CTXT1.include_output_string)
                    {

                        output_string += (int)(byte*)CTXT1.OldBufferOffset;
                    }
                }
                else
                {
                    if (OutputPair(match_position, match_length - 2, output_string, ref CTXT1) == 0)
                    {
                        return 0;
                    }
                    if (CTXT1.include_output_string)
                    {
                        output_string += (int)(byte*)CTXT1.OldBufferOffset;
                    }
                    replace_count = match_length;
                }
                for (i = 0; i < replace_count; i++)
                {
                    DeleteString(MOD_WINDOW(CurrentPosition + LOOK_AHEAD_SIZE), ref CTXT1);
                    c = input_string[0];
                    size--;
                    if (size < 0)
                    {
                        look_ahead_bytes--;
                    }
                    else
                    {
                        input_string++;
                        CTXT1.window[MOD_WINDOW(CurrentPosition + LOOK_AHEAD_SIZE)] = c;
                    }
                    CurrentPosition = MOD_WINDOW(CurrentPosition + 1);
                    if (look_ahead_bytes != 0)
                    {
                        match_length = AddString(CurrentPosition, ref match_position, ref CTXT1);
                    }
                }
            }
            if (!CTXT1.include_output_string)
            {
                FlushOutputBuffer(output_string, ref CTXT1);
            }
            return CTXT1.compressed_size;
        }
        /// <summary>
        /// Initializes the tree structure used for Look Ahead and building the dictionary
        /// </summary>
        /// <param name="r"></param>
        /// <param name="CTXT1"></param>
        private static void InitTree(int r, ref LZSS_COMP_CTXT CTXT1)
        {
            for (int i = 0; i < 0x1001; i++)
            {
                CTXT1.tree[i].parent = 0;
                CTXT1.tree[i].larger_child = 0;
                CTXT1.tree[i].smaller_child = 0;
            }
            CTXT1.tree[0x1000].larger_child = r;
            CTXT1.tree[r].parent = 0x1000;
            CTXT1.tree[r].larger_child = 0;
            CTXT1.tree[r].smaller_child = 0;
        }
        /// <summary>
        /// Flushes the output buffer after data is moved to the correct location 
        /// </summary>
        /// <param name="output_string"></param>
        /// <param name="CTXT1"></param>
        /// <returns></returns>
        private static unsafe int FlushOutputBuffer(byte* output_string, ref LZSS_COMP_CTXT CTXT1)
        {
            if (CTXT1.BufferOffset != 1)
            {
                for (int i = 0; i < CTXT1.BufferOffset; i++)
                {
                    output_string[i] = CTXT1.DataBuffer[i];
                }
                CTXT1.compressed_size += (int)CTXT1.BufferOffset;
                InitOutputBuffer(ref CTXT1);
            }
            return 1;
        }
        /// <summary>
        /// Entry Point for Compression Operations
        /// </summary>
        /// <param name="uncompressedContent">Data being compressed</param>
        /// <returns></returns>
        public static unsafe byte[] Compress(byte[] uncompressedContent)
        {
            byte[] destinationArray = new byte[uncompressedContent.Length + 100];
            Array.Copy(uncompressedContent, destinationArray, uncompressedContent.Length);
            int length = 0;
            fixed (byte* numRef = uncompressedContent)
            {
                fixed (byte* numRef2 = destinationArray)
                {
                    length = Compress(numRef, numRef2, uncompressedContent.Length);
                }
            }
            byte[] buffer2 = new byte[length];
            Array.Copy(destinationArray, buffer2, length);
            return buffer2;
        }
        /// <summary>
        /// Entry Point for Decompression Operations
        /// </summary>
        /// <param name="compressedContent">Compressed data to be decompressed</param>
        /// <param name="uncompressedSize">Target size for Decompressed Data</param>
        /// <returns></returns>
        public static unsafe byte[] Decompress(byte[] compressedContent, int uncompressedSize)
        {
            byte[] destinationArray = new byte[compressedContent.Length + 100];
            Array.Copy(compressedContent, destinationArray, compressedContent.Length);
            byte[] sourceArray = new byte[uncompressedSize + 0x400];
            fixed (byte* numRef = sourceArray)
            {
                fixed (byte* numRef2 = destinationArray)
                {
                    int num = Expand(numRef2, numRef, uncompressedSize);
                }
            }
            byte[] buffer3 = new byte[uncompressedSize];
            Array.Copy(sourceArray, buffer3, uncompressedSize);
            return buffer3;
        }
        /// <summary>
        /// Data Buffer (LZSS Compression Context)
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct LZSS_COMP_CTXT
        {
            public byte[] window;
            public TreeNode[] tree;
            public byte[] DataBuffer;
            public int FlagBitMask;
            public uint BufferOffset;
            public uint OldBufferOffset;
            public int original_size;
            public int compressed_size;
            public bool include_input_string;
            public bool include_output_string;
            public void Initialize()
            {
                window = new byte[WINDOW_SIZE];
                tree = new TreeNode[WINDOW_SIZE + 1];
                DataBuffer = new byte[LOOK_AHEAD_SIZE];
            }
        }
        /// <summary>
        /// Data Node within the buffer stored in a tree for Look Ahead anlysis
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct TreeNode
        {
            public int parent;
            public int smaller_child;
            public int larger_child;
        }
    }
}
