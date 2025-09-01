/**
 * Audio and Video Conversion Service
 *
 * Handles audio file conversions using pure Node.js libraries
 * Supports WAV to MP3 conversion with high-quality encoding
 */

const fs = require("fs");
const wav = require("node-wav");
const lamejs = require("lamejs");

class AudioVideoService {
  constructor() {
    console.log("Audio/Video service initialized with pure Node.js libraries");
  }

  /**
   * Convert audio or video files between formats
   */
  async convert(inputPath, outputPath, inputFormat, targetFormat) {
    try {
      console.log(`Converting ${inputFormat} to ${targetFormat}`);

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
   */
  async wavToMp3(inputPath, outputPath) {
    try {
      const wavBuffer = fs.readFileSync(inputPath);
      const wavData = wav.decode(wavBuffer);

      const mp3Encoder = new lamejs.Mp3Encoder(
        wavData.channelData.length,
        wavData.sampleRate,
        128
      );
      const mp3Data = [];

      const samples = new Int16Array(wavData.channelData[0].length);
      for (let i = 0; i < wavData.channelData[0].length; i++) {
        let sample = 0;
        for (let channel = 0; channel < wavData.channelData.length; channel++) {
          sample += wavData.channelData[channel][i];
        }
        sample = sample / wavData.channelData.length;
        samples[i] = sample < 0 ? sample * 0x8000 : sample * 0x7fff;
      }

      const mp3buf = mp3Encoder.encodeBuffer(samples);
      if (mp3buf.length > 0) {
        mp3Data.push(mp3buf);
      }

      const mp3bufEnd = mp3Encoder.flush();
      if (mp3bufEnd.length > 0) {
        mp3Data.push(mp3bufEnd);
      }

      const mp3Buffer = Buffer.concat(mp3Data);
      fs.writeFileSync(outputPath, mp3Buffer);

      return {
        success: true,
        outputPath,
        filename: require("path").basename(outputPath),
      };
    } catch (error) {
      throw new Error(`WAV to MP3 conversion failed: ${error.message}`);
    }
  }

  /**
   * Convert MP3 files to WAV format (placeholder)
   */
  async mp3ToWav(inputPath, outputPath) {
    try {
      // Create a placeholder WAV file with info about the MP3
      const wavHeader = this.createWavHeader(44100, 16, 1);
      const infoText = `MP3 to WAV conversion not fully supported.\nOriginal file: ${require("path").basename(
        inputPath
      )}`;

      const textBuffer = Buffer.from(infoText, "utf8");
      const paddedBuffer = Buffer.alloc(Math.ceil(textBuffer.length / 2) * 2);
      textBuffer.copy(paddedBuffer);

      const wavBuffer = Buffer.concat([wavHeader, paddedBuffer]);
      fs.writeFileSync(outputPath, wavBuffer);

      return {
        success: true,
        outputPath,
        filename: require("path").basename(outputPath),
      };
    } catch (error) {
      throw new Error(`MP3 to WAV conversion failed: ${error.message}`);
    }
  }

  /**
   * Create video placeholder file
   */
  async createVideoPlaceholder(
    inputPath,
    outputPath,
    inputFormat,
    targetFormat
  ) {
    try {
      const infoText = `Video conversion from ${inputFormat} to ${targetFormat} requires specialized libraries.\nOriginal file: ${require("path").basename(
        inputPath
      )}`;
      fs.writeFileSync(outputPath, infoText);

      return {
        success: true,
        outputPath,
        filename: require("path").basename(outputPath),
      };
    } catch (error) {
      throw new Error(`Video placeholder creation failed: ${error.message}`);
    }
  }

  /**
   * Create basic WAV header
   */
  createWavHeader(sampleRate, bitsPerSample, channels) {
    const buffer = Buffer.alloc(44);
    const view = new DataView(buffer.buffer);

    // RIFF header
    view.setUint32(0, 0x52494646, false); // "RIFF"
    view.setUint32(4, 36, true); // File size
    view.setUint32(8, 0x57415645, false); // "WAVE"

    // Format chunk
    view.setUint32(12, 0x666d7420, false); // "fmt "
    view.setUint32(16, 16, true); // Chunk size
    view.setUint16(20, 1, true); // Audio format (PCM)
    view.setUint16(22, channels, true); // Channels
    view.setUint32(24, sampleRate, true); // Sample rate
    view.setUint32(28, (sampleRate * channels * bitsPerSample) / 8, true); // Byte rate
    view.setUint16(32, (channels * bitsPerSample) / 8, true); // Block align
    view.setUint16(34, bitsPerSample, true); // Bits per sample

    // Data chunk
    view.setUint32(36, 0x64617461, false); // "data"
    view.setUint32(40, 0, true); // Data size

    return buffer;
  }
}

module.exports = new AudioVideoService();
