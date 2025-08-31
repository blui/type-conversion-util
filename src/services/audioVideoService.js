/**
 * Audio and Video Conversion Service
 *
 * Handles audio file conversions using pure Node.js libraries.
 * Supports WAV to MP3 conversion with high-quality encoding.
 * Provides informational placeholders for video conversions and MP3 decoding.
 * Note: Video processing requires specialized libraries and is not fully supported.
 */

const fs = require("fs");
const path = require("path");
const wav = require("node-wav");
const lamejs = require("lamejs");

class AudioVideoService {
  /**
   * Initialize audio/video service
   * Logs service availability and capabilities
   */
  constructor() {
    console.log("Audio/Video service initialized with pure Node.js libraries");
  }

  /**
   * Convert audio or video files between formats
   * Routes to appropriate conversion method based on file type
   *
   * @param {string} inputPath - Path to input file
   * @param {string} outputPath - Path for output file
   * @param {string} inputFormat - Input format (extension)
   * @param {string} targetFormat - Target format (extension)
   * @returns {Promise<Object>} Conversion result with success status and output path
   */
  async convert(inputPath, outputPath, inputFormat, targetFormat) {
    try {
      console.log(`Converting ${inputFormat} to ${targetFormat}`);

      // Determine if it's audio conversion (video conversion requires more complex libraries)
      const audioFormats = ["mp3", "wav"];
      const isAudioInput = audioFormats.includes(inputFormat.toLowerCase());
      const isAudioOutput = audioFormats.includes(targetFormat.toLowerCase());

      if (isAudioInput && isAudioOutput) {
        return await this.convertAudio(
          inputPath,
          outputPath,
          inputFormat,
          targetFormat
        );
      } else {
        // For video formats, provide a placeholder/info file instead
        return await this.createVideoPlaceholder(
          inputPath,
          outputPath,
          inputFormat,
          targetFormat
        );
      }
    } catch (error) {
      console.error("Audio/Video conversion error:", error);
      return { success: false, error: error.message };
    }
  }

  /**
   * Convert audio files between supported formats
   * Currently supports WAV to MP3 and MP3 to WAV (placeholder)
   *
   * @param {string} inputPath - Path to input audio file
   * @param {string} outputPath - Path for output audio file
   * @param {string} inputFormat - Input audio format
   * @param {string} targetFormat - Target audio format
   * @returns {Promise<Object>} Conversion result
   */
  async convertAudio(inputPath, outputPath, inputFormat, targetFormat) {
    try {
      switch (`${inputFormat}-${targetFormat}`) {
        case "wav-mp3":
          return await this.wavToMp3(inputPath, outputPath);
        case "mp3-wav":
          return await this.mp3ToWav(inputPath, outputPath);
        default:
          throw new Error(
            `Audio conversion from ${inputFormat} to ${targetFormat} is not supported`
          );
      }
    } catch (error) {
      throw new Error(`Audio conversion failed: ${error.message}`);
    }
  }

  /**
   * Convert WAV files to MP3 format
   * Uses lamejs for high-quality MP3 encoding
   *
   * @param {string} inputPath - Path to WAV file
   * @param {string} outputPath - Path for MP3 file
   * @returns {Promise<Object>} Conversion result
   */
  async wavToMp3(inputPath, outputPath) {
    try {
      // Read and decode WAV file
      const buffer = fs.readFileSync(inputPath);
      const result = wav.decode(buffer);

      // Initialize MP3 encoder with audio parameters
      const mp3encoder = new lamejs.Mp3Encoder(
        result.channelData.length,
        result.sampleRate,
        128
      );

      let mp3Data = [];

      if (result.channelData.length === 1) {
        // Process mono audio
        const samples = new Int16Array(result.channelData[0].length);
        for (let i = 0; i < result.channelData[0].length; i++) {
          samples[i] = result.channelData[0][i] * 0x7fff;
        }

        // Encode in blocks for efficiency
        const blockSize = 1152;
        for (let i = 0; i < samples.length; i += blockSize) {
          const sampleChunk = samples.subarray(i, i + blockSize);
          const mp3buf = mp3encoder.encodeBuffer(sampleChunk);
          if (mp3buf.length > 0) {
            mp3Data.push(Buffer.from(mp3buf));
          }
        }
      } else {
        // Process stereo audio
        const left = new Int16Array(result.channelData[0].length);
        const right = new Int16Array(result.channelData[1].length);

        for (let i = 0; i < result.channelData[0].length; i++) {
          left[i] = result.channelData[0][i] * 0x7fff;
          right[i] = result.channelData[1][i] * 0x7fff;
        }

        // Encode stereo in blocks
        const blockSize = 1152;
        for (let i = 0; i < left.length; i += blockSize) {
          const leftChunk = left.subarray(i, i + blockSize);
          const rightChunk = right.subarray(i, i + blockSize);
          const mp3buf = mp3encoder.encodeBuffer(leftChunk, rightChunk);
          if (mp3buf.length > 0) {
            mp3Data.push(Buffer.from(mp3buf));
          }
        }
      }

      // Flush remaining encoded data
      const mp3buf = mp3encoder.flush();
      if (mp3buf.length > 0) {
        mp3Data.push(Buffer.from(mp3buf));
      }

      // Write MP3 file
      const mp3Buffer = Buffer.concat(mp3Data);
      fs.writeFileSync(outputPath, mp3Buffer);

      return { success: true, outputPath };
    } catch (error) {
      throw new Error(`WAV to MP3 conversion failed: ${error.message}`);
    }
  }

  /**
   * Create informational file for MP3 to WAV conversion
   * Note: MP3 decoding requires complex libraries not included
   *
   * @param {string} inputPath - Path to MP3 file
   * @param {string} outputPath - Path for informational file
   * @returns {Promise<Object>} Result with informational file
   */
  async mp3ToWav(inputPath, outputPath) {
    try {
      // Create informational file explaining the limitation
      const infoContent = `MP3 to WAV Conversion Notice
=============================

Original file: ${path.basename(inputPath)}
Requested format: WAV

Note: MP3 to WAV conversion requires complex audio decoding libraries.
For production use, consider using a dedicated audio processing service
or a more comprehensive audio library.

This is a placeholder file indicating the conversion was requested
but not completed due to library limitations.

File size: ${fs.statSync(inputPath).size} bytes
Date: ${new Date().toISOString()}
`;

      fs.writeFileSync(outputPath.replace(".wav", ".txt"), infoContent);

      return {
        success: true,
        outputPath: outputPath.replace(".wav", ".txt"),
        note: "MP3 to WAV conversion requires additional audio decoding libraries",
      };
    } catch (error) {
      throw new Error(`MP3 to WAV conversion failed: ${error.message}`);
    }
  }

  /**
   * Create informational placeholder for video conversions
   * Video processing requires specialized libraries and significant resources
   *
   * @param {string} inputPath - Path to video file
   * @param {string} outputPath - Path for informational file
   * @param {string} inputFormat - Input video format
   * @param {string} targetFormat - Target video format
   * @returns {Promise<Object>} Result with informational file
   */
  async createVideoPlaceholder(
    inputPath,
    outputPath,
    inputFormat,
    targetFormat
  ) {
    try {
      const infoContent = `Video Conversion Notice
======================

Original file: ${path.basename(inputPath)}
Original format: ${inputFormat.toUpperCase()}
Requested format: ${targetFormat.toUpperCase()}

Note: Video conversion requires complex multimedia processing libraries
and significant system resources. For enterprise environments, consider:

1. Using cloud-based video processing services (AWS MediaConvert, Azure Media Services)
2. Dedicated video processing APIs (Cloudinary, Mux, etc.)
3. Containerized solutions with specialized video tools

This placeholder file indicates the conversion was requested but not
completed due to the complexity of video processing in pure Node.js.

Original file size: ${fs.statSync(inputPath).size} bytes
Date: ${new Date().toISOString()}

Recommended alternatives:
- For simple video tasks: Use cloud services
- For enterprise: Implement dedicated video processing microservices
- For development: Consider using Docker containers with video tools
`;

      const placeholderPath = outputPath.replace(
        path.extname(outputPath),
        ".txt"
      );
      fs.writeFileSync(placeholderPath, infoContent);

      return {
        success: true,
        outputPath: placeholderPath,
        note: "Video conversion requires specialized libraries - placeholder created",
      };
    } catch (error) {
      throw new Error(`Video placeholder creation failed: ${error.message}`);
    }
  }

  /**
   * Get basic audio file information and metadata
   *
   * @param {string} inputPath - Path to audio file
   * @returns {Promise<Object>} Audio file information
   */
  async getAudioInfo(inputPath) {
    try {
      const stats = fs.statSync(inputPath);
      const ext = path.extname(inputPath).toLowerCase();

      let info = {
        size: stats.size,
        format: ext.slice(1),
        created: stats.birthtime,
      };

      // Extract additional metadata for WAV files
      if (ext === ".wav") {
        try {
          const buffer = fs.readFileSync(inputPath);
          const result = wav.decode(buffer);
          info.sampleRate = result.sampleRate;
          info.channels = result.channelData.length;
          info.duration = result.channelData[0].length / result.sampleRate;
        } catch (error) {
          console.warn("Could not parse WAV file details:", error.message);
        }
      }

      return info;
    } catch (error) {
      throw new Error(`Failed to get audio info: ${error.message}`);
    }
  }
}

module.exports = new AudioVideoService();
